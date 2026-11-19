import json, struct, math, io
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Exports/Mixer'; OUT.mkdir(parents=True,exist_ok=True)
S=2048; W=.149; D=.202; TOP=.035
rng=np.random.default_rng(42)
noise=np.clip(rng.normal(28,1.7,(S,S)),0,255).astype('uint8')
tex=Image.fromarray(np.stack([noise,noise+3,noise+4],axis=-1),'RGB'); d=ImageDraw.Draw(tex)
fontpath='C:/Windows/Fonts/arial.ttf'
def text(x,z,s,size=25,fill='#b7c4c7'):
    f=ImageFont.truetype(fontpath,size); d.text((x*S,z*S),s,font=f,fill=fill,anchor='mm')
def line(points,fill='#0093cc',width=4): d.line([(int(x*S),int(z*S)) for x,z in points],fill=fill,width=width)
def rect(x,z,w,h,fill): d.rounded_rectangle((x*S,z*S,(x+w)*S,(z+h)*S),radius=8,fill=fill)
for x in [.035,.205,.375,.595]: line([(x,.02),(x,.98)])
rect(.605,.012,.36,.325,'#0088be')
for a in range(8): line([(.62,.018+a*.006),(.95,.018+a*.006)],'#172326',3)
for z,label in [(.107,'3/4 LINE'),(.245,'5/6 LINE')]:
    text(.485,z+.07,label,29); text(.43,z+.036,'L / MONO',18); text(.54,z+.036,'R',18)
for x,label in [(.12,'1 / L'),(.29,'2 / R')]:
    text(x,.209,label,32); text(x,.235,'MIC / LINE',26)
    text(x-.035,.265,'PAD',19); text(x+.04,.265,'HPF',19)
    text(x-.035,.323,'26 dB',18); text(x+.04,.323,'80 Hz',18)
for x in [.695,.855]: text(x,.208,'L' if x<.7 else 'R',26)
text(.77,.232,'STEREO OUT',27); text(.91,.266,'PHONES',22)
text(.77,.39,'COMPACT MIXER',33); text(.77,.416,'6 INPUT / STEREO',21)
rect(.39,.441,.32,.046,'#074159'); text(.54,.461,'PHANTOM +48V',22)
rect(.392,.495,.32,.125,'#10272d'); text(.55,.515,'REVERB / DELAY',24)
for z,l,r in [(.545,'HALL','SHORT'),(.575,'ROOM','LONG'),(.605,'PLATE','ECHO')]:
    text(.46,z,l,17); text(.66,z,r,17)
rect(.737,.592,.20,.20,'#080c0d')
text(.837,.61,'L       dB       R',18)
for i,label in enumerate(['PEAK','+6','+3','0','-3','-10','-20']):
    z=.638+i*.021; text(.836,z,label,18)
    for x in [.765,.882]: rect(x,z-.003,.032,.006, '#e5ac13' if i<4 else '#128145')
for x in [.12,.29]:
    for z,label in [(.477,'GAIN'),(.572,'HIGH / 10 kHz'),(.666,'LOW / 100 Hz'),(.855,'LEVEL')]: text(x,z-.05,label,22)
    text(x,.932,'1 / L' if x<.2 else '2 / R',30)
    rect(x-.047,.711,.094,.053,'#0089c0'); text(x,.752,'FX',18)
    text(x,.793,'PEAK',18)
for x,z,label in [(.465,.690,'LEVEL 3/4'),(.465,.855,'LEVEL 5/6'),(.64,.690,'FX RETURN'),(.64,.855,'PHONES'),(.825,.855,'STEREO')]:
    if x>.6: rect(x-.075,z-.065,.15,.18,'#008ac0')
    text(x,z+.065,label,24)
for x,z in [(x,z) for x in [.12,.29] for z in [.477,.572,.666,.855]]+[(.465,.690),(.465,.855),(.64,.690),(.64,.855),(.825,.855)]:
    for a in np.linspace(-225,45,21):
        r=math.radians(a); line([(x+math.cos(r)*.045,z+math.sin(r)*.033),(x+math.cos(r)*.053,z+math.sin(r)*.039)],'#acb8b9',3)
for x in [.64,.825]:
    for i in range(6): line([(x-.065,.942+i*.006),(x+.065,.942+i*.006)],'#152327',4)
tex.save(OUT/'mixer_panel.png')

materials=[]
def mat(name,color,metal=0,rough=.5,texture=False,emissive=None):
    p={'baseColorFactor':[*color,1],'metallicFactor':metal,'roughnessFactor':rough}
    if texture:p['baseColorTexture']={'index':0}
    m={'name':name,'pbrMetallicRoughness':p}
    if emissive:m['emissiveFactor']=emissive
    materials.append(m); return len(materials)-1
black=mat('Powder coated charcoal',[.025,.032,.035],.35,.68)
rubber=mat('Soft black rubber',[.009,.013,.015],0,.82)
silver=mat('Brushed nickel sockets',[.64,.68,.69],.86,.24)
white=mat('Ivory knob caps',[.86,.88,.83],.05,.32)
green=mat('Green EQ caps',[.005,.64,.19],.05,.4)
red=mat('Red master cap',[.82,.022,.014],.05,.34)
blue=mat('Blue selector',[.0,.43,.68],.2,.4)
gold=mat('Gold connector pins',[.7,.48,.12],.8,.23)
panel=mat('Printed textured faceplate',[1,1,1],.28,.65,True)
led=mat('Indicator light',[1,.035,.015],0,.25,emissive=[.65,.01,.005])
meshes=[]
def mesh(name,v,n,idx,m,uv=None): meshes.append((name,np.array(v,dtype='<f4'),np.array(n,dtype='<f4'),np.array(idx,dtype='<u4'),m,None if uv is None else np.array(uv,dtype='<f4')))
def box(name,x,y,z,sx,sy,sz,m):
    v=[]; n=[]; ids=[]
    for normal,corners in [((0,1,0),[(-1,1,-1),(-1,1,1),(1,1,1),(1,1,-1)]),((0,-1,0),[(-1,-1,1),(-1,-1,-1),(1,-1,-1),(1,-1,1)]),((1,0,0),[(1,-1,-1),(1,1,-1),(1,1,1),(1,-1,1)]),((-1,0,0),[(-1,-1,1),(-1,1,1),(-1,1,-1),(-1,-1,-1)]),((0,0,1),[(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]),((0,0,-1),[(1,-1,-1),(-1,-1,-1),(-1,1,-1),(1,1,-1)])]:
        k=len(v);v.extend([(x+a*sx/2,y+b*sy/2,z+c*sz/2) for a,b,c in corners]);n.extend([normal]*4);ids.extend([k,k+1,k+2,k,k+2,k+3])
    mesh(name,v,n,ids,m)
def cyl(name,x,y,z,r,h,m,inner=0,segments=48):
    v=[]; n=[]; ids=[]
    def quad(points,normals):
        k=len(v);v.extend(points);n.extend(normals);ids.extend([k,k+1,k+2,k,k+2,k+3])
    for i in range(segments):
        a=i*2*math.pi/segments;b=(i+1)*2*math.pi/segments
        ca,sa,cb,sb=math.cos(a),math.sin(a),math.cos(b),math.sin(b)
        quad([(x+r*ca,y,z+r*sa),(x+r*ca,y+h,z+r*sa),(x+r*cb,y+h,z+r*sb),(x+r*cb,y,z+r*sb)],[(ca,0,sa)]*2+[(cb,0,sb)]*2)
        quad([(x+inner*ca,y+h,z+inner*sa),(x+inner*cb,y+h,z+inner*sb),(x+r*cb,y+h,z+r*sb),(x+r*ca,y+h,z+r*sa)],[(0,1,0)]*4)
        if inner:quad([(x+inner*cb,y,z+inner*sb),(x+inner*cb,y+h,z+inner*sb),(x+inner*ca,y+h,z+inner*sa),(x+inner*ca,y,z+inner*sa)],[(-cb,0,-sb)]*2+[(-ca,0,-sa)]*2)
    mesh(name,v,n,ids,m)
def pos(u,v):return (u-.5)*W,(v-.5)*D
box('Metal enclosure',0,.017,0,W,.034,D,black)
for x in [-W/2,W/2]:box('Protective side rail',x,.020,0,.006,.042,D+.002,rubber)
mesh('Printed faceplate',[(-W/2,TOP,-D/2),(-W/2,TOP,D/2),(W/2,TOP,D/2),(W/2,TOP,-D/2)],[(0,1,0)]*4,[0,1,2,0,2,3],panel,[(0,0),(0,1),(1,1),(1,0)])
for u,v in [(.12,.125),(.29,.125),(.695,.125),(.855,.125)]:
    x,z=pos(u,v);cyl('XLR mounting flange',x,TOP,z,.0105,.0018,rubber,inner=.0088);cyl('Recessed connector well',x,TOP+.0001,z,.0086,.0005,rubber)
    cyl('XLR socket barrel',x,TOP+.0006,z,.009,.0028,black,inner=.0077)
    for a in [30,150,270]:
        px=x+.004*math.cos(math.radians(a));pz=z+.004*math.sin(math.radians(a))
        cyl('XLR contact pin' if u>.6 else 'XLR contact opening',px,TOP+.001,pz,.00065,.003 if u>.6 else .0003,gold if u>.6 else rubber)
for u,v in [(.43,.107),(.54,.107),(.43,.245),(.54,.245),(.66,.295),(.775,.295),(.91,.295)]:
    x,z=pos(u,v);cyl('Jack dark bore',x,TOP+.0002,z,.0045,.0004,rubber);cyl('Jack hex mounting nut',x,TOP+.0004,z,.0062,.0015,silver,inner=.0044,segments=6);cyl('Quarter inch socket rim',x,TOP+.0019,z,.0051,.0013,silver,inner=.0038)
knobs=[(u,v,green if v in [.572,.666] else white) for u in [.12,.29] for v in [.477,.572,.666,.855]]+[(.465,.690,white),(.465,.855,white),(.64,.690,white),(.64,.855,white),(.825,.855,red)]
for k,(u,v,cap) in enumerate(knobs):
    x,z=pos(u,v);r=.0058 if cap==green else .0068
    cyl(f'Knob {k+1} rubber grip',x,TOP,z,r,.007,rubber)
    for a in range(0,360,30):
        a=math.radians(a);cyl('Grip flute',x+math.cos(a)*r*.94,TOP+.001,z+math.sin(a)*r*.94,.00065,.0055,black,segments=8)
    cyl('Colored knob cap',x,TOP+.007,z,r*.73,.0008,cap)
    box('Knob pointer',x-.0012,TOP+.0079,z+.002,.00065,.0002,.0038,rubber)
for u in [.085,.16,.255,.33]:
    x,z=pos(u,.291);box('Switch bezel',x,TOP+.0005,z,.0085,.001,.007,rubber);box('Pad / high pass switch',x,TOP+.002,z,.0068,.003,.0055,black)
for u,v,m in [(.12,.732,black),(.29,.732,black),(.65,.461,red),(.55,.53,black),(.55,.58,blue),(.82,.493,black)]:
    x,z=pos(u,v);box('Switch surround',x,TOP+.0005,z,.009,.001,.007,rubber);box('Control switch',x,TOP+.002,z,.007,.003,.005,m)
for u,v in [(.07,.793),(.24,.793),(.59,.461)]:
    x,z=pos(u,v);cyl('Status LED',x,TOP,z,.0014,.0015,led,segments=24)
for u,v in [(.046,.04),(.95,.04),(.046,.46),(.95,.46),(.046,.94),(.95,.94)]:
    x,z=pos(u,v);cyl('Panel screw',x,TOP,z,.0021,.0008,silver,segments=24);box('Screw slot',x,TOP+.00085,z,.0026,.0001,.0004,rubber)
for x in [-.055,.055]:
    for z in [-.08,.08]:cyl('Rubber foot',x,-.004,z,.007,.005,rubber,segments=24)
for x in np.linspace(-.045,.045,18):box('Rear ventilation slot',x,.021,-D/2-.0001,.0015,.010,.0003,rubber)

blob=bytearray(); views=[]; access=[]; gm=[]; nodes=[]
def data(b,target=None):
    while len(blob)%4:blob.append(0)
    v={'buffer':0,'byteOffset':len(blob),'byteLength':len(b)}
    if target:v['target']=target
    views.append(v);blob.extend(b);return len(views)-1
def acc(a,typ,component):
    ix=data(a.tobytes(),34963 if typ=='SCALAR' else 34962);o={'bufferView':ix,'componentType':component,'count':len(a),'type':typ}
    if typ=='VEC3':o.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
    access.append(o);return len(access)-1
for name,v,n,idx,m,uv in meshes:
    attrs={'POSITION':acc(v,'VEC3',5126),'NORMAL':acc(n,'VEC3',5126)}
    if uv is not None:attrs['TEXCOORD_0']=acc(uv,'VEC2',5126)
    gm.append({'name':name,'primitives':[{'attributes':attrs,'indices':acc(idx,'SCALAR',5125),'material':m}]});nodes.append({'name':name,'mesh':len(gm)-1})
png=io.BytesIO();tex.save(png,format='PNG');imview=data(png.getvalue())
g={'asset':{'version':'2.0','generator':'Unbranded compact mixer builder','extras':{'units':'meters','description':'Original unbranded 6-input mixer inspired by supplied layout'}},'scene':0,'scenes':[{'nodes':list(range(len(nodes)))}],'nodes':nodes,'meshes':gm,'materials':materials,'textures':[{'source':0,'sampler':0}],'samplers':[{'magFilter':9729,'minFilter':9987,'wrapS':33071,'wrapT':33071}],'images':[{'bufferView':imview,'mimeType':'image/png'}],'accessors':access,'bufferViews':views,'buffers':[{'byteLength':len(blob)}]}
j=json.dumps(g,separators=(',',':')).encode();j+=b' '*((-len(j))%4);blob+=b'\0'*((-len(blob))%4)
glb=struct.pack('<III',0x46546c67,2,12+8+len(j)+8+len(blob))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(blob),0x004e4942)+blob
(OUT/'UnbrandedMixer.glb').write_bytes(glb)
print(f'Exported {len(glb):,} bytes; {len(meshes)} parts; {sum(len(m[3])//3 for m in meshes):,} triangles')
# Validate every exported accessor and binary view.
assert struct.unpack_from('<I',glb,8)[0]==len(glb)
for v in views:assert v['byteOffset']+v['byteLength']<=len(blob)
for _,v,n,idx,_,uv in meshes:assert np.isfinite(v).all() and idx.max()<len(v) and len(n)==len(v)
(OUT/'README.txt').write_text('Unbranded compact audio mixer\n\nUnbrandedMixer.glb: self-contained glTF 2.0 binary with embedded 2048px panel texture and PBR materials.\nDimensions approximately 155 x 204 x 48 mm. Y up; front at +Z; meters.\nSeparate named mesh parts for knobs, sockets, switches, enclosure and feet.\nNo logos, manufacturer names, or model branding.\nRebuild: run Tools/MixerModel/build_mixer.py with Python, Pillow and NumPy.\n')
# Orthographic preview with a depth buffer, using the same geometry and texture.
N=1000
canvas=np.full((N,N,3),235,dtype=np.uint8); depth=np.full((N,N),-np.inf)
eye=np.array([.45,.95,.70]);eye/=np.linalg.norm(eye)
right=np.cross(np.array([0,1,0]),eye);right/=np.linalg.norm(right)
up=np.cross(eye,right); basis=np.array([right,up,eye])
light=np.array([-.4,1,.3]);light/=np.linalg.norm(light)
ta=np.asarray(tex)
for name,verts,norms,indices,mi,uv in meshes:
    p=verts@basis.T;p[:,:2]*=3500;p[:,0]+=N/2;p[:,1]=N*.53-p[:,1]
    base=np.array(materials[mi]['pbrMetallicRoughness']['baseColorFactor'][:3])
    for tri in indices.reshape(-1,3):
        q=p[tri]; lo=np.maximum(np.floor(q[:,:2].min(0)).astype(int),0);hi=np.minimum(np.ceil(q[:,:2].max(0)).astype(int),N-1)
        if np.any(hi<lo):continue
        xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1))
        a,b,c=q;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-8:continue
        u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
        v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v
        zz=u*a[2]+v*b[2]+w*c[2]; sl=np.s_[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
        mask=(u>=-1e-5)&(v>=-1e-5)&(w>=-1e-5)&(zz>depth[sl])
        if not mask.any():continue
        normal=norms[tri].mean(0);shade=.48+.52*max(0,float(normal@light))
        if uv is not None:
            coord=u[...,None]*uv[tri[0]]+v[...,None]*uv[tri[1]]+w[...,None]*uv[tri[2]]
            tx=np.clip((coord[...,0]*(S-1)).astype(int),0,S-1);ty=np.clip((coord[...,1]*(S-1)).astype(int),0,S-1)
            color=np.clip(ta[ty,tx]*shade,0,255).astype('uint8')
        else:color=np.broadcast_to(np.clip(base**(1/2.2)*255*shade,0,255).astype('uint8'),(*u.shape,3))
        canvas[sl][mask]=color[mask];depth[sl][mask]=zz[mask]
Image.fromarray(canvas).save(OUT/'mixer_preview.png')
print('Geometry validation passed; preview rendered.')
