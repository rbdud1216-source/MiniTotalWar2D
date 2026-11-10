const fs = require('fs');
const path = require('path');

const twDataDir = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data';
const terrainPackPath = path.join(twDataDir, 'terrain.pack');
const outGrassBaseDir = 'C:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation\\DetailGrass';

function decodeFloat16(binary) {
    const exponent = (binary & 0x7C00) >> 10;
    const fraction = binary & 0x03FF;
    const sign = (binary & 0x8000) > 0 ? -1 : 1;
    if (exponent === 0) return sign * Math.pow(2, -14) * (fraction / 1024);
    else if (exponent === 0x1F) return fraction ? NaN : sign * Infinity;
    return sign * Math.pow(2, exponent - 15) * (1 + fraction / 1024);
}

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

function convertDetailGrassRmv2ToObj(rmv2Buf, modelName) {
    const magic = rmv2Buf.toString('ascii', 0, 4);
    if (magic !== 'RMV2') throw new Error(`Not RMV2: ${magic}`);

    const lodCount = rmv2Buf.readUInt16LE(8);
    let cur = 140;
    const lod0_meshCount = rmv2Buf.readUInt32LE(cur);
    cur += 28 * lodCount;

    const objLines = [
        `# Total War Three Kingdoms Converted Groundcover Grass Mesh`,
        `# Model: ${modelName}`,
        `mtllib ${modelName}.mtl`,
        `o ${modelName}`,
        `g Leaves`,
        `usemtl Leaves_Mat`
    ];

    let globalVertOffset = 1;

    for (let m = 0; m < lod0_meshCount; m++) {
        const meshStart = cur;
        const matType = rmv2Buf.readUInt16LE(cur);
        const sectionSize = rmv2Buf.readUInt32LE(cur + 4);
        const vOffset = rmv2Buf.readUInt32LE(cur + 8);
        const vCount = rmv2Buf.readUInt32LE(cur + 12);
        const iOffset = rmv2Buf.readUInt32LE(cur + 16);
        const iCount = rmv2Buf.readUInt32LE(cur + 20);

        const vSize = (iOffset - vOffset) / vCount;
        const gVStart = meshStart + vOffset;
        const gIStart = meshStart + iOffset;

        for (let i = 0; i < vCount; i++) {
            const ptr = gVStart + i * vSize;

            let x, y, z;
            let u = 0, v = 0;

            if (vSize === 28) {
                // DetailGrass 28-byte layout: Half3 pos at 0,2,4, Half1 w at 6, Float2 UV at 8,12
                x = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 0));
                y = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 2));
                z = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 4));
                u = rmv2Buf.readFloatLE(ptr + 8);
                v = 1.0 - rmv2Buf.readFloatLE(ptr + 12);
            } else {
                // Fallback standard layout
                x = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 0));
                y = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 2));
                z = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 4));
                u = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 8));
                v = 1.0 - decodeFloat16(rmv2Buf.readUInt16LE(ptr + 10));
            }

            // 토탈워 좌표계: y가 높이이거나 z가 높이일 수 있으므로 그대로 또는 정규화
            objLines.push(`v ${x.toFixed(4)} ${y.toFixed(4)} ${z.toFixed(4)}`);
            objLines.push(`vt ${u.toFixed(4)} ${v.toFixed(4)}`);
            objLines.push(`vn 0.0000 1.0000 0.0000`);
        }

        for (let i = 0; i < iCount; i += 3) {
            const idx1 = rmv2Buf.readUInt16LE(gIStart + i * 2) + globalVertOffset;
            const idx2 = rmv2Buf.readUInt16LE(gIStart + (i + 1) * 2) + globalVertOffset;
            const idx3 = rmv2Buf.readUInt16LE(gIStart + (i + 2) * 2) + globalVertOffset;
            objLines.push(`f ${idx1}/${idx1}/${idx1} ${idx2}/${idx2}/${idx2} ${idx3}/${idx3}/${idx3}`);
        }

        globalVertOffset += vCount;
        cur += sectionSize;
    }

    const mtlLines = [
        `# Material file for ${modelName}`,
        `newmtl Leaves_Mat`,
        `Ka 1.0 1.0 1.0`,
        `Kd 1.0 1.0 1.0`,
        `Ks 0.0 0.0 0.0`,
        `d 1.0`,
        `illum 2`
    ];

    return {
        obj: objLines.join('\n'),
        mtl: mtlLines.join('\n')
    };
}

console.log('--- RECONVERTING ALL DETAIL GRASS FROM terrain.pack ---');
const terr = readPackIndex(terrainPackPath);

let convertedCount = 0;
for (const entry of terr.packEntries) {
    const p = entry.path.toLowerCase();
    if (p.endsWith('.rigid_model_v2') && p.includes('terrain/vegetation/battle/grass/')) {
        const rawFilename = path.basename(entry.path, '.rigid_model_v2'); // ard_grass0_1

        let climateDir = 'Climate_Temperate';
        if (rawFilename.startsWith('ard_')) climateDir = 'Climate_Arid';
        else if (rawFilename.startsWith('nan_')) climateDir = 'Climate_Nanman';
        else if (rawFilename.startsWith('cld_')) climateDir = 'Climate_Cold';
        else if (rawFilename.startsWith('sbt_')) climateDir = 'Climate_Subtropical';
        else if (rawFilename.startsWith('tmp_')) climateDir = 'Climate_Temperate';

        const targetFolder = path.join(outGrassBaseDir, climateDir);
        if (!fs.existsSync(targetFolder)) fs.mkdirSync(targetFolder, { recursive: true });

        const buf = Buffer.alloc(entry.size);
        fs.readSync(terr.fd, buf, 0, entry.size, entry.offset);

        try {
            const outName = rawFilename.split('_').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join('_');
            const res = convertDetailGrassRmv2ToObj(buf, outName);

            fs.writeFileSync(path.join(targetFolder, `${outName}.obj`), res.obj, 'utf8');
            fs.writeFileSync(path.join(targetFolder, `${outName}.mtl`), res.mtl, 'utf8');

            convertedCount++;
            if (convertedCount % 20 === 0 || convertedCount <= 5) {
                console.log(`[Reconverted ${convertedCount}] ${climateDir}/${outName}.obj`);
            }
        } catch (err) {
            console.error(`Error on ${entry.path}: ${err.message}`);
        }
    }
}

fs.closeSync(terr.fd);
console.log(`--- TOTAL RECONVERTED DETAIL GRASS: ${convertedCount} models! ---`);
