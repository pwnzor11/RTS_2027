# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Booklet-only colour grades (session 33, owner: "no pink"). The game's own images are not changed.

paint(im)        painted scenes: magenta and pink dusk casts become warm ash amber (sky region looser, the ground keeps its reds).
house(im, hex)   hero renders: the magenta house-colour panels become the faction's display colour, keeping the shading.
"""
import numpy as np
from PIL import Image
def _hsv(rgb):
    r,g,b=[rgb[...,i] for i in range(3)]; mx=rgb.max(-1); mn=rgb.min(-1); d=mx-mn
    h=np.zeros_like(mx); nz=d>1e-6
    rm=nz&(mx==r); gm=nz&(mx==g)&~rm; bm=nz&~rm&~gm
    h[rm]=((g-b)[rm]/d[rm])%6; h[gm]=(b-r)[gm]/d[gm]+2; h[bm]=(r-g)[bm]/d[bm]+4
    h*=60; s=np.where(mx>0,d/np.maximum(mx,1e-6),0); return h,s,mx
def _rgb(h,s,v):
    c=v*s; x=c*(1-np.abs((h/60)%2-1)); m=v-c; z=np.zeros_like(h); k=(h//60).astype(int)%6
    out=np.zeros(h.shape+(3,))
    for i,(a,b,cc) in enumerate([(c,x,z),(x,c,z),(z,c,x),(z,x,c),(x,z,c),(c,z,x)]):
        sel=k==i; out[sel]=np.stack([a[sel],b[sel],cc[sel]],-1)
    return out+m[...,None]
def house(im, hexc):
    """Recolour the magenta house panels of a render (hue 280..345) to a display colour, keeping each pixel's shading."""
    im=im.convert('RGBA'); a=np.asarray(im).astype(float)/255; h,s,v=_hsv(a[...,:3])
    th,ts,tv=_hsv(np.array([[[int(hexc[i:i+2],16)/255 for i in (1,3,5)]]]))
    m=(a[...,3]>0)&(h>275)&(h<350)&(s>0.18)
    hh=np.where(m,th[0,0],h); ss=np.where(m,np.clip(s*ts[0,0]/0.5,0,1),s)
    rgb=_rgb(hh,ss,v); out=np.concatenate([rgb,a[...,3:]],-1)
    return Image.fromarray((out*255).clip(0,255).astype(np.uint8),'RGBA')

def _lab(rgb):
    c=np.where(rgb<=0.04045,rgb/12.92,((rgb+0.055)/1.055)**2.4)
    M=np.array([[0.4124,0.3576,0.1805],[0.2126,0.7152,0.0722],[0.0193,0.1192,0.9505]])
    xyz=c@M.T/np.array([0.95047,1.0,1.08883])
    f=np.where(xyz>0.008856,np.cbrt(xyz),7.787*xyz+16/116)
    return np.stack([116*f[...,1]-16,500*(f[...,0]-f[...,1]),200*(f[...,1]-f[...,2])],-1)
def _unlab(lab):
    L,a,b=lab[...,0],lab[...,1],lab[...,2]; fy=(L+16)/116; fx=fy+a/500; fz=fy-b/200
    f=np.stack([fx,fy,fz],-1); xyz=np.where(f**3>0.008856,f**3,(f-16/116)/7.787)*np.array([0.95047,1.0,1.08883])
    Mi=np.array([[3.2406,-1.5372,-0.4986],[-0.9689,1.8758,0.0415],[0.0557,-0.2040,1.0570]])
    c=xyz@Mi.T; c=np.clip(c,0,1); return np.where(c<=0.0031308,12.92*c,1.055*c**(1/2.4)-0.055)
def paint(im):
    """Booklet-only grade: magenta/pink casts (Lab a > 0 with b not warmer) turn into warm ash amber; blues and fire stay."""
    rgb=np.asarray(im.convert('RGB')).astype(float)/255; lab=_lab(rgb); L,a,b=lab[...,0],lab[...,1],lab[...,2]
    y=np.linspace(0,1,a.shape[0])[:,None]; sky=np.clip((0.62-y)/0.25,0,1)
    strict=np.clip((0.55*a-b)/8,0,1); loose=np.clip((a-0.75*b)/12,0,1)
    w=np.maximum(strict,loose*sky)*np.clip(a/4,0,1)
    a2=a*(1-0.75*w); b2=b+w*(0.8*a+3)
    out=_unlab(np.stack([L,a2,b2],-1)); return Image.fromarray((out*255).astype(np.uint8))
