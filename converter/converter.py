from os.path import exists, join
from zipfile import ZipFile

from glob import glob
from buffer import Buffer
from s3d import readS3D
from wld import Wld
from zon import readZon
from zonefile import *

def s3dFallback(*filedicts):
    ndicts = []
    for files in filedicts:
        nfiles = {}
        ndicts.append(nfiles)
        for xfiles in filedicts:
            if xfiles is not files:
                nfiles.update(xfiles)
        nfiles.update(files)
    return ndicts

def eqPath(fn):
    return join(eqdata, fn).replace('\\', '/')

def convertOld(name):
    with ZipFile('%s.zip' % name, 'w') as zip:
        zone = Zone()
        objfiles = {}
        for fn in glob(eqPath('%s_obj*.s3d' % name)):
            objfiles[fn.split('/')[-1][:-4]] = readS3D(file(fn, 'rb'))
        zfiles = readS3D(file(eqPath('%s.s3d' % name), 'rb'))
        flists = objfiles.values() + [zfiles]
        flists = s3dFallback(*flists)
        for i, fn in enumerate(objfiles.keys()):
            objfiles[fn] = flists[i]
        zfiles = flists[-1]

        for fn, sf in objfiles.items():
            print fn
            Wld(sf['%s.wld' % fn], sf).convertObjects(zone)
        Wld(zfiles['objects.wld'], zfiles).convertObjects(zone)
        Wld(zfiles['lights.wld'], zfiles).convertLights(zone)
        Wld(zfiles['%s.wld' % name], zfiles).convertZone(zone)
        zone.output(zip)

def convertChr(name):
    name = name[:-4]
    files = readS3D(file(eqPath('%s_chr.s3d' % name), 'rb'))
    with ZipFile('%s_chr.zip' % name, 'w') as zip:
        Wld(files['%s_chr.wld' % name], files).convertCharacters(zip)

def convertNew(name):
    with ZipFile('%s.zip' % name, 'w') as zip:
        zone = Zone()
        zfiles = readS3D(file(eqPath('%s.eqg' % name), 'rb'))
        #for fn, data in zfiles.items():
        #    file('s3data/%s' % fn, 'wb').write(data)
        if '%s.zon' % name in zfiles:
            readZon(zfiles['%s.zon' % name], zone, zfiles)
        else:
            readZon(file(eqPath('%s.zon' % name), 'rb').read(), zone, zfiles)
        zone.output(zip)

def main(name):
    global eqdata, config

    with file('VisualEQ.cfg', 'r') as fp:
        configdata = fp.read()

    config = dict([x.strip() for x in line.split('=', 1)] for line in [x.split('#', 1)[0] for x in configdata.split('\n')] if '=' in line)

    eqdata = config['eqdata']

    if '_chr' in name:
        convertChr(name)
    elif exists(eqPath('%s.s3d' % name)):
        convertOld(name)
    elif exists(eqPath('%s.eqg' % name)):
        convertNew(name)
    else:
        print 'Cannot find zone'
        return
    print 'All Done'

if __name__=='__main__':
    import sys
    main(*sys.argv[1:])
