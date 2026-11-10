const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const twDataDir = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data';
const terrainPackPath = path.join(twDataDir, 'terrain.pack');
const outTexDir = 'C:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation\\Textures';

function readPackIndex(packPath) {
    const fd = fs.openSync(packPath, 'r');
    const headerBuf = Buffer.alloc(28);
    fs.readSync(fd, headerBuf, 0, 28, 0);
    const fileCount = headerBuf.readUInt32LE(16);
    const indexSize = headerBuf.readUInt32LE(20);
    const indexBuf = Buffer.alloc(indexSize);
    fs.readSync(fd, indexBuf, 0, indexSize, 28);
    let cursor = 0;
    let currentOffset = 28 + indexSize;

    const packEntries = [];
    for (let i = 0; i < fileCount; i++) {
        const size = indexBuf.readUInt32LE(cursor);
        cursor += 5;
        let end = cursor;
        while (indexBuf[end] !== 0) end++;
        const p = indexBuf.toString('utf8', cursor, end).replace(/\\/g, '/');
        cursor = end + 1;
        packEntries.push({ path: p, size, offset: currentOffset });
        currentOffset += size;
    }
    return { fd, packEntries };
}

// 1. Convert shrub_grassclump_ha_base_colour.dds if exists
const haDds = path.join(outTexDir, 'shrub_grassclump_ha_base_colour.dds');
const haPng = path.join(outTexDir, 'shrub_grassclump_ha_base_colour.png');
if (fs.existsSync(haDds) && !fs.existsSync(haPng)) {
    try {
        execSync(`ffmpeg -y -i "${haDds}" "${haPng}"`, { stdio: 'ignore' });
        console.log('Converted: shrub_grassclump_ha_base_colour.png');
    } catch (e) {
        console.error('Failed ha:', e.message);
    }
}

// 2. Extract grass textures from terrain.pack
const terr = readPackIndex(terrainPackPath);
for (const entry of terr.packEntries) {
    const p = entry.path.toLowerCase();
    if (p.includes('terrain/vegetation/battle/grass/textures/') && p.endsWith('.dds')) {
        const fname = path.basename(entry.path);
        const outDds = path.join(outTexDir, fname);
        if (!fs.existsSync(outDds)) {
            const buf = Buffer.alloc(entry.size);
            fs.readSync(terr.fd, buf, 0, entry.size, entry.offset);
            if (buf.length > 148 && buf.toString('ascii', 80, 84) === 'DX10') {
                buf.writeUInt32LE(1, 144);
            }
            fs.writeFileSync(outDds, buf);
            console.log('Extracted DDS:', fname);
        }
        if (fname.includes('base_colour') || fname.includes('normal')) {
            const outPng = outDds.replace(/\.dds$/i, '.png');
            if (!fs.existsSync(outPng)) {
                try {
                    execSync(`ffmpeg -y -i "${outDds}" "${outPng}"`, { stdio: 'ignore' });
                    console.log('Converted PNG:', path.basename(outPng));
                } catch (e) {
                    console.error('FFmpeg failed for', fname, e.message);
                }
            }
        }
    }
}
fs.closeSync(terr.fd);
