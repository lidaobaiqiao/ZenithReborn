"""数值检查原型截面和合并函数的边界性质；不替代 GPU 或画面评审。"""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
output = root / 'artifacts/liquid-glass-prototype/numerical-check.json'
output.parent.mkdir(parents=True, exist_ok=True)
k = 0.012

def ridge(d):
    return max(0.0, 1.0 - d*d)**3

def smoothstep01(x):
    x = min(1.0, max(0.0, x))
    return x*x*(3.0-2.0*x)

def smooth_max(a, b):
    difference = max(0.0, 1.0-abs(a-b)/k)
    return max(a,b) + (k/4)*smoothstep01(min(a,b)/k)*difference*difference

def derivative(function, x, delta):
    return (function(x+delta)-function(x-delta))/(2*delta)

# 从内外逼近条带边界；中心和边界的斜率都应趋近零。
epsilon = 1e-5
edge_value = ridge(1-epsilon)
edge_gradient = derivative(ridge, 1.0, epsilon)
center_gradient = derivative(ridge, 0.0, epsilon)
assert edge_value < 1e-12 and abs(edge_gradient) < 1e-8 and center_gradient == 0
assert ridge(1+epsilon) == 0 and ridge(-1-epsilon) == 0

# 人工交叉：两条表面在 x=0 等高但斜率相反，严格 max 必然留下折痕。
def a(x): return 0.04 + 0.5*x
def b(x): return 0.04 - 0.25*x
def hard(x): return max(a(x),b(x))
def soft(x): return smooth_max(a(x),b(x))
left_hard = (hard(0)-hard(-epsilon))/epsilon
right_hard = (hard(epsilon)-hard(0))/epsilon
left_soft = (soft(0)-soft(-epsilon))/epsilon
right_soft = (soft(epsilon)-soft(0))/epsilon
assert abs(left_hard+0.25) < 1e-9 and abs(right_hard-0.5) < 1e-9
assert abs(right_soft-left_soft) < 0.001

# 当两面高度都趋近零时，修正量也趋近零；每次合并有严格上界。
tiny = [k*10**(-i) for i in range(1,8)]
base_lifts = [smooth_max(v,v)-v for v in tiny]
assert all(x>y>0 for x,y in zip(base_lifts,base_lifts[1:]))
assert base_lifts[-1] < 1e-14 and smooth_max(0,0) == 0
max_lift=0.0
max_three_lift=0.0
for ia in range(101):
    for ib in range(101):
        av=ia*0.0012; bv=ib*0.0012
        first=smooth_max(av,bv)
        max_lift=max(max_lift, first-max(av,bv))
        for cv in (0.0, 0.006, 0.012, 0.03, 0.06, 0.12):
            combined=smooth_max(first,cv)
            max_three_lift=max(max_three_lift,combined-max(av,bv,cv))
assert max_lift <= k/4+1e-12 and max_three_lift <= k/2+1e-12

result={
    'passed': True,
    'method': '独立标量数值性质检查；曲面参数与合并公式对应隔离 Shader；非 GPU 结果',
    'cross_section_edge_inside_height':edge_value,
    'cross_section_edge_centered_slope':edge_gradient,
    'cross_section_center_slope':center_gradient,
    'hard_crossing_left_right_slopes':[left_hard,right_hard],
    'smooth_crossing_left_right_slopes':[left_soft,right_soft],
    'near_base_height_and_lift':list(zip(tiny,base_lifts)),
    'sampled_max_lift_two':max_lift,
    'sampled_max_lift_three':max_three_lift,
    'proven_per_merge_lift_bound':k/4,
    'proven_two_merge_lift_bound':k/2,
    'boundary_note':'接近基面时权重渐退，避免高度跳变，但渐退区交叉处不能保证法线完全连续。'
}
output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
