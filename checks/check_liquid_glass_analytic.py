"""对隔离液态刀光解析梯度做独立标量差分核对，不代表 GPU 或外观通过。"""
import json
import math
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
K = 0.012


def strip(p, angle, origin, bend, width, height, length, phase, flow):
    c, s = math.cos(angle), math.sin(angle)
    x, y = p[0] - origin[0], p[1] - origin[1]
    along, across = x*c+y*s, -x*s+y*c
    center = bend*math.sin(along*2.3+phase)
    local_width = width*(1+0.12*math.sin(along*1.7+phase*0.8))
    d = (across-center)/local_width
    cross = max(0.0, 1-d*d)
    section = max(0.0, 1-(along/length)**2)
    base_amp = 0.70+0.30*math.sin(along*4.4+phase*0.65)
    flow_amp = 0.60+0.25*math.sin(along*2.4+phase*0.65)+0.15*math.sin(along*4.1-phase*0.43)
    amplitude = height*(base_amp*(1-flow)+flow_amp*flow)
    center_prime = bend*2.3*math.cos(along*2.3+phase)
    width_prime = width*0.12*1.7*math.cos(along*1.7+phase*0.8)
    base_prime = 0.30*4.4*math.cos(along*4.4+phase*0.65)
    flow_prime = 0.25*2.4*math.cos(along*2.4+phase*0.65)+0.15*4.1*math.cos(along*4.1-phase*0.43)
    amplitude_prime = height*(base_prime*(1-flow)+flow_prime*flow)
    dd_along = (-center_prime-d*width_prime)/local_width
    cross_prime = -6*d*cross*cross
    section_prime = -6*along*section*section/(length*length)
    h = amplitude*cross**3*section**3
    ga = amplitude_prime*cross**3*section**3 + amplitude*cross**3*section_prime + amplitude*section**3*cross_prime*dd_along
    gb = amplitude*section**3*cross_prime/local_width
    return h, ga*c-gb*s, ga*s+gb*c


def merge(a,b,smooth):
    top, lower = (a,b) if a[0]>=b[0] else (b,a)
    pos = min(1,max(0,lower[0]/K))
    overlap = pos*pos*(3-2*pos)
    delta = a[0]-b[0]
    blend = min(1,max(0,1-abs(delta)/K))
    sign = 1 if delta>0 else (-1 if delta<0 else 0)
    top_g = ((a[1]+b[1])/2,(a[2]+b[2])/2) if delta==0 else top[1:]
    overlap_g = tuple((6/K)*pos*(1-pos)*x for x in lower[1:])
    blend_g = tuple((-sign/K)*(a[i]-b[i]) if abs(delta)<K else 0 for i in (1,2))
    lift = smooth*overlap*K*.25*blend*blend
    grad = tuple(top_g[i]+smooth*K*.25*(overlap_g[i]*blend*blend+overlap*2*blend*blend_g[i]) for i in (0,1))
    return top[0]+lift,*grad


def field(p,t,smooth):
    a=strip(p,-.09,(-.80,-.105),.075,.140,.060,1.10,t*.50,1)
    b=strip(p,.10,(.10,.080),.070,.145,.055,.95,t*.39+1.4,1)
    c=strip(p,-.07,(1.00,-.025),.085,.135,.050,1.05,t*.43+2.6,1)
    base=strip(p,.035,(0,0),.07,.55,.012,2.35,t*.19+.7,0)
    return merge(merge(a,b,smooth),merge(c,base,smooth),smooth)


random.seed(1029)
results={}
step=1e-6
for smooth in (0,1):
    errors=[]
    for _ in range(10000):
        x=random.uniform(-1.9,1.9); y=random.uniform(-.48,.48); t=random.uniform(0,4)
        h,gx,gy=field((x,y),t,smooth)
        dx=(field((x+step,y),t,smooth)[0]-field((x-step,y),t,smooth)[0])/(2*step)
        dy=(field((x,y+step),t,smooth)[0]-field((x,y-step),t,smooth)[0])/(2*step)
        errors.append(max(abs(dx-gx),abs(dy-gy)))
    errors.sort()
    # 严格最大值的等高线不可微；极少数差分跨接缝的样本不作为平滑梯度误差。
    p999=errors[int(len(errors)*.999)-1]
    if p999>1e-5:
        raise AssertionError(f'解析梯度与数值差分不符: smooth={smooth}, p999={p999}')
    results[str(smooth)]={'samples':len(errors),'p999AbsoluteError':p999,'maximumAbsoluteError':errors[-1]}

out={'passed':True,'method':'独立标量中心差分；严格最大值的等高线不可微，极少数跨线样本单独保留最大误差','step':step,'results':results}
target=ROOT/'artifacts/liquid-glass-trail-prototype/analytic-gradient-numerical-check.json'
target.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(out,ensure_ascii=False,indent=2))
