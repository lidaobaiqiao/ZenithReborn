from pathlib import Path
from PIL import Image, ImageOps
import re,json,hashlib,html
root=Path('VfxLibrary/Vanilla'); images=root/'Images'; thumbs=root/'Thumbnails'; thumbs.mkdir(parents=True,exist_ok=True)
source=Path(r'E:\SteamLibrary\steamapps\common\Terraria\Content\Images')
def ids(path):
 s=Path(path).read_text(encoding='utf-8-sig',errors='replace'); out={}
 for name,num in re.findall(r'public const (?:short|int|ushort|byte)\s+(\w+)\s*=\s*(\d+)\s*;',s):
  if name not in ('Count','None'):out.setdefault(int(num),[]).append(name)
 return out
maps={'弹幕':ids('tools/decomp/Terraria.ID.ProjectileID.decompiled.cs'),'额外特效':ids('artifacts/vanilla-ExtrasID.reference.cs'),'碎片':ids('artifacts/vanilla-GoreID.reference.cs'),'粒子':ids('artifacts/vanilla-DustID.reference.cs')}
keywords=[('星辰','star','stellar','nebula','solar','lunar','moon','celestial'),('神圣','hallow','holy','sacred','divine','angel'),('剑与斩击','sword','blade','slash','swing','spear','swordbeam'),('光束','beam','laser','ray','lightning','bolt'),('火焰','fire','flame','inferno','burn'),('冰雪','ice','frost','snow','blizzard'),('爆炸','explosion','explode','blast'),('魔法','magic','magic','mana','mystic')]
def tag(text):
 t=text.lower();return ' '.join(label for label,*terms in keywords if any(w in t for w in terms))
rows=[]
def add(p,cat,name,number=None,aliases=None,original=None):
 try:
  im=Image.open(p).convert('RGBA'); im.thumbnail((128,128),Image.Resampling.NEAREST)
  tp=thumbs/(p.relative_to(images) if p.is_relative_to(images) else Path(cat)/(p.stem+'.png'));tp.parent.mkdir(parents=True,exist_ok=True);im.save(tp,optimize=True)
  size=Image.open(p).size; alias=' / '.join(aliases or []); ident=p.relative_to(images).as_posix().removesuffix('.png') if p.is_relative_to(images) and len(p.relative_to(images).parts)>2 else p.stem
  rows.append({'c':cat,'n':ident,'i':number,'a':alias,'t':tag(ident+' '+alias),'w':size[0],'h':size[1],
   'p':p.relative_to(root).as_posix(),'s':tp.relative_to(root).as_posix(),'o':original or ''})
 except Exception as e: raise RuntimeError(str(p)) from e
for p in sorted(images.rglob('*.png')):
 cat=p.relative_to(images).parts[0]; m=re.search(r'_(\d+)$',p.stem); num=int(m.group(1)) if m else None
 add(p,cat,p.stem,num,maps.get(cat,{}).get(num,[]))
atlas=Image.open(images/'粒子图集'/'Dust.png').convert('RGBA'); dustdir=root/'DustByID';dustdir.mkdir(exist_ok=True)
for i in sorted(maps['粒子']):
 if i<0 or i>=400:continue
 x=(i%100)*10;y=(i//100)*30
 strip=Image.new('RGBA',(24,8))
 for frame in range(3):strip.paste(atlas.crop((x,y+frame*10,x+8,y+frame*10+8)),(frame*8,0))
 p=dustdir/f'Dust_{i}.png';strip.save(p);add(p,'粒子',p.stem,i,maps['粒子'][i],'Dust.xnb')
rows.sort(key=lambda r:(r['c'], r['i'] if r['i'] is not None else -1, r['n']))
(root/'catalog.js').write_text('window.VANILLA_VFX='+json.dumps(rows,ensure_ascii=False,separators=(',',':'))+';',encoding='utf-8')
counts={}
for r in rows:counts[r['c']]=counts.get(r['c'],0)+1
(root/'manifest.json').write_text(json.dumps({'source':str(source),'source_type':'Terraria 原版安装目录 XNB','raw_png_count':sum(1 for _ in images.rglob('*.png')),'dust_slices':counts.get('粒子',0),'counts':counts,'notes':['贴图为原始素材，不等于实际动画或完整战斗效果','粒子按原版8×8帧从Dust图集裁剪，三帧并排','英文标识来自本机 tModLoader ID 定义，部分资源无名称映射','按照文件类型归档，分类并不保证每张图都可作为特效']},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(counts,ensure_ascii=False))
