"""Pure-stdlib GIF assembler for slash preview frames (no PIL)."""
import struct, zlib, sys, os

def read_png(path):
    with open(path,'rb') as f:
        data=f.read()
    assert data[:8]==b'\x89PNG\r\n\x1a\n'
    pos=8; w=h=bitdepth=colortype=None; idat=b''
    while pos<len(data):
        length,=struct.unpack('>I',data[pos:pos+4]); ctype=data[pos+4:pos+8]
        chunk=data[pos+8:pos+8+length]
        if ctype==b'IHDR':
            w,h,bitdepth,colortype,comp,filt,interlace=struct.unpack('>IIBBBBB',chunk)
        elif ctype==b'IDAT':
            idat+=chunk
        elif ctype==b'IEND':
            break
        pos+=12+length
    raw=zlib.decompress(idat)
    ch={0:1,2:3,3:1,4:2,6:4}[colortype]
    stride=w*ch
    out=bytearray()
    prev=bytearray(stride)
    p=0
    for y in range(h):
        ftype=raw[p]; p+=1
        line=bytearray(raw[p:p+stride]); p+=stride
        if ftype==0: pass
        elif ftype==1:
            for i in range(ch,stride): line[i]=(line[i]+line[i-ch])&0xff
        elif ftype==2:
            for i in range(stride): line[i]=(line[i]+prev[i])&0xff
        elif ftype==3:
            for i in range(stride): line[i]=(line[i]+((line[i-ch] if i>=ch else 0)+prev[i])//2)&0xff
        elif ftype==4:
            for i in range(stride):
                a=line[i-ch] if i>=ch else 0; b=prev[i]; c=prev[i-ch] if i>=ch else 0
                pp=a+b-c; pa=abs(pp-a); pb=abs(pp-b); pc=abs(pp-c)
                pr=a if (pa<=pb and pa<=pc) else (b if pb<=pc else c)
                line[i]=(line[i]+pr)&0xff
        out.extend(line); prev=line
    return w,h,bytes(out),ch

def quantize(px,w,h,ch,maxc=256):
    idx=bytearray(w*h)
    pal=[]; seen={}
    for i in range(w*h):
        r=px[i*ch]&0xe0; g=px[i*ch+1]&0xe0; b=px[i*ch+2]&0xe0
        key=(r,g,b)
        if key not in seen:
            if len(pal)>=256:
                idx[i]=seen[(pal[0][0],pal[0][1],pal[0][2])]
                continue
            seen[key]=len(pal); pal.append((r,g,b))
        idx[i]=seen[key]
    return bytes(idx),pal

def lzw_encode(idx, min_code):
    clear = 1 << min_code
    eoi = clear + 1
    next_code = eoi + 1
    code_size = min_code + 1
    table = {bytes([c]): c for c in range(clear)}
    out = bytearray()
    buf = 0; bufbits = 0
    def emit(code):
        nonlocal buf, bufbits
        buf |= code << bufbits
        bufbits += code_size
        while bufbits >= 8:
            out.append(buf & 0xff)
            buf >>= 8
            bufbits -= 8
    emit(clear)
    prefix = bytes([idx[0]])
    for i in range(1, len(idx)):
        c = idx[i]
        combined = prefix + bytes([c])
        if combined in table:
            prefix = combined
        else:
            emit(table[prefix])
            table[combined] = next_code
            next_code += 1
            if next_code > (1 << code_size) and code_size < 12:
                code_size += 1
            if next_code > 4095:
                emit(clear)
                table = {bytes([c]): c for c in range(clear)}
                next_code = eoi + 1
                code_size = min_code + 1
            prefix = bytes([c])
    emit(table[prefix])
    emit(eoi)
    if bufbits > 0:
        out.append(buf & 0xff)
    return bytes(out)

def write_gif(frames,outpath,delay=30):
    # frames: list of (idx_bytes, palette_list, w, h)
    w,h=frames[0][2],frames[0][3]
    with open(outpath,'wb') as f:
        f.write(b'GIF89a')
        # logical screen descriptor
        f.write(struct.pack('<HHBBB',w,h,0xf7,0,0))
        # global palette (use first frame's palette padded to 256)
        pal=frames[0][1][:256]
        while len(pal)<256: pal.append((0,0,0))
        for r,g,b in pal: f.write(bytes((r,g,b)))
        # NETSCAPE ext
        f.write(b'\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00')
        for idx,_,_,_ in frames:
            f.write(b'\x21\xf9\x04')
            f.write(bytes((0,)))
            f.write(struct.pack('<H',delay))
            f.write(bytes((0,0)))
            f.write(b'\x2c')
            f.write(struct.pack('<HHHHB',0,0,w,h,0))
            mincode=8
            f.write(bytes((mincode,)))
            data=lzw_encode(idx,mincode)
            # sub-blocks
            pos=0
            while pos<len(data):
                chunk=data[pos:pos+255]; f.write(bytes((len(chunk),))); f.write(chunk); pos+=255
            f.write(b'\x00')
        f.write(b'\x3b')

folder=sys.argv[1]
files=sorted([os.path.join(folder,f) for f in os.listdir(folder) if f.startswith('slash-') and f.endswith('.png')])
# only take slash-001..slash-072
files=[f for f in files if os.path.basename(f).replace('slash-','').replace('.png','').isdigit()]
files=files[:72]
print(f'{len(files)} frames')
frames=[]
for fp in files:
    w,h,px,ch=read_png(fp)
    idx,pal=quantize(px,w,h,ch)
    frames.append((idx,pal,w,h))
out=os.path.join(folder,'slash-preview.gif')
write_gif(frames,out)
print(out)
