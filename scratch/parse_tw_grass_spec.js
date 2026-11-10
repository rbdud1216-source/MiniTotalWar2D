const fs = require('fs');
const path = require('path');

const twDataDir = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data';
const packPath = path.join(twDataDir, 'terrain.pack');
const fd = fs.openSync(packPath, 'r');
const headerBuf = Buffer.alloc(28);
fs.readSync(fd, headerBuf, 0, 28, 0);
const fileCount = headerBuf.readUInt32LE(16);
const indexSize = headerBuf.readUInt32LE(20);
const indexBuf = Buffer.alloc(indexSize);
fs.readSync(fd, indexBuf, 0, indexSize, 28);
let cursor = 0;
let currentOffset = 28 + indexSize;

for (let i = 0; i < fileCount; i++) {
    const size = indexBuf.readUInt32LE(cursor);
    cursor += 5;
    let end = cursor;
    while (indexBuf[end] !== 0) end++;
    const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
    cursor = end + 1;
    if (p.includes('grass_generation_spec.xml')) {
        const buf = Buffer.alloc(size);
        fs.readSync(fd, buf, 0, size, currentOffset);
        const text = buf.toString('utf8');
        
        const climates = ['subtropical', 'temperate', 'arid', 'cold'];
        for (const c of climates) {
            const regex = new RegExp('<climate values="' + c + '"[\\s\\S]*?<\\/climate>', 'gi');
            let match;
            const models = new Set();
            let textureLayers = 0;
            while ((match = regex.exec(text)) !== null) {
                textureLayers++;
                const mRegex = /<model[^>]*>([^<]+)<\/model>/gi;
                let mMatch;
                while ((mMatch = mRegex.exec(match[0])) !== null) {
                    const mName = mMatch[1].trim();
                    if (!mName.startsWith('<!--')) {
                        models.add(path.basename(mName));
                    }
                }
            }
            console.log('=== Climate:', c, '===');
            console.log('Texture Layers count:', textureLayers);
            console.log('Unique DetailGrass Models in single climate:', models.size);
            console.log('Models sample:', Array.from(models).slice(0, 8));
        }
        break;
    }
    currentOffset += size;
}
