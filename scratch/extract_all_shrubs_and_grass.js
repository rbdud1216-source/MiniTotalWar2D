const fs = require('fs');
const path = require('path');

const twDataDir = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data';
const vegPackPath = path.join(twDataDir, 'vegetation.pack');
const terrainPackPath = path.join(twDataDir, 'terrain.pack');

const outBaseDir = 'C:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation';
const shrubsDir = path.join(outBaseDir, 'Shrubs');
const grassDir = path.join(outBaseDir, 'DetailGrass');
const texturesDir = path.join(outBaseDir, 'Textures');

if (!fs.existsSync(shrubsDir)) fs.mkdirSync(shrubsDir, { recursive: true });
if (!fs.existsSync(grassDir)) fs.mkdirSync(grassDir, { recursive: true });
if (!fs.existsSync(texturesDir)) fs.mkdirSync(texturesDir, { recursive: true });

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

function decodeFloat16(binary) {
    const exponent = (binary & 0x7C00) >> 10;
    const fraction = binary & 0x03FF;
    const sign = (binary & 0x8000) > 0 ? -1 : 1;
    if (exponent === 0) return sign * Math.pow(2, -14) * (fraction / 1024);
    else if (exponent === 0x1F) return fraction ? NaN : sign * Infinity;
    return sign * Math.pow(2, exponent - 15) * (1 + fraction / 1024);
}

function convertRmv2ToObj(rmv2Buf, modelName) {
    const magic = rmv2Buf.toString('ascii', 0, 4);
    if (magic !== 'RMV2') throw new Error(`Not an RMV2 file: ${magic}`);

    const lodCount = rmv2Buf.readUInt16LE(8);
    const skeleton = rmv2Buf.toString('utf8', 12, 140).replace(/\0/g, '');

    let cur = 140;
    const lod0_meshCount = rmv2Buf.readUInt32LE(cur);
    cur += 28 * lodCount;

    const submeshes = [];

    for (let m = 0; m < lod0_meshCount; m++) {
        const meshStart = cur;
        const matType = rmv2Buf.readUInt16LE(cur);
        const sectionSize = rmv2Buf.readUInt32LE(cur + 4);
        const vOffset = rmv2Buf.readUInt32LE(cur + 8);
        const vCount = rmv2Buf.readUInt32LE(cur + 12);
        const iOffset = rmv2Buf.readUInt32LE(cur + 16);
        const iCount = rmv2Buf.readUInt32LE(cur + 20);

        const isTrunk = (matType === 74);

        const globalVStart = meshStart + vOffset;
        const vSize = (iOffset - vOffset) / vCount;
        const globalIStart = meshStart + iOffset;

        const verts = [];
        const uvs = [];
        const normals = [];

        for (let i = 0; i < vCount; i++) {
            const ptr = globalVStart + i * vSize;

            let x = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 8));
            let y = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 10));
            let z = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 12));

            if (x === 0 && y === 0 && z === 0) {
                const x0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 0));
                const y0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 2));
                const z0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 4));
                if (x0 !== 0 || y0 !== 0 || z0 !== 0) {
                    x = x0; y = y0; z = z0;
                }
            }
            verts.push([x, y, z]);

            let nx = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 16));
            let ny = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 18));
            let nz = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 20));
            if (isNaN(nx) || !isFinite(nx)) nx = 0;
            if (isNaN(ny) || !isFinite(ny)) ny = 1;
            if (isNaN(nz) || !isFinite(nz)) nz = 0;
            normals.push([nx, ny, nz]);

            let u = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 40));
            let v = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 42));
            if (isNaN(u) || !isFinite(u)) u = 0;
            if (isNaN(v) || !isFinite(v)) v = 0;
            uvs.push([u, 1.0 - v]);
        }

        const indices = [];
        for (let i = 0; i < iCount; i++) {
            indices.push(rmv2Buf.readUInt16LE(globalIStart + i * 2));
        }

        submeshes.push({
            matType: matType,
            isTrunk: isTrunk,
            verts: verts,
            normals: normals,
            uvs: uvs,
            indices: indices
        });

        cur = meshStart + sectionSize;
    }

    submeshes.sort((a, b) => (b.isTrunk ? 1 : 0) - (a.isTrunk ? 1 : 0));

    let objLines = [
        `# Total War Three Kingdoms Converted Mesh`,
        `# Model: ${modelName}`,
        `# Skeleton: ${skeleton}`,
        `mtllib ${modelName}.mtl`,
        `o ${modelName}`
    ];

    let globalVertOffset = 1;

    for (let s = 0; s < submeshes.length; s++) {
        const sm = submeshes[s];
        let groupName = sm.isTrunk ? 'Trunk' : (s === 1 ? 'Leaves' : 'Twigs');
        if (submeshes.length === 1) groupName = 'Leaves';
        objLines.push(`g ${groupName}`);
        objLines.push(`usemtl ${groupName}_Mat`);

        for (const [x, y, z] of sm.verts) {
            objLines.push(`v ${x.toFixed(4)} ${y.toFixed(4)} ${z.toFixed(4)}`);
        }
        for (const [u, v] of sm.uvs) {
            objLines.push(`vt ${u.toFixed(4)} ${v.toFixed(4)}`);
        }
        for (const [nx, ny, nz] of sm.normals) {
            objLines.push(`vn ${nx.toFixed(4)} ${ny.toFixed(4)} ${nz.toFixed(4)}`);
        }
        for (let i = 0; i < sm.indices.length; i += 3) {
            const i1 = sm.indices[i] + globalVertOffset;
            const i2 = sm.indices[i + 1] + globalVertOffset;
            const i3 = sm.indices[i + 2] + globalVertOffset;
            objLines.push(`f ${i1}/${i1}/${i1} ${i2}/${i2}/${i2} ${i3}/${i3}/${i3}`);
        }
        globalVertOffset += sm.verts.length;
    }

    const mtlLines = [
        `# Material file for ${modelName}`,
        `newmtl Trunk_Mat`,
        `Ka 1.0 1.0 1.0`,
        `Kd 1.0 1.0 1.0`,
        `Ks 0.0 0.0 0.0`,
        `d 1.0`,
        `illum 2`,
        ``,
        `newmtl Leaves_Mat`,
        `Ka 1.0 1.0 1.0`,
        `Kd 1.0 1.0 1.0`,
        `Ks 0.0 0.0 0.0`,
        `d 1.0`,
        `illum 2`,
        ``,
        `newmtl Twigs_Mat`,
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

// 1. Process vegetation.pack (Shrubs & Foliage)
console.log('--- Processing vegetation.pack ---');
const veg = readPackIndex(vegPackPath);

let shrubExtractedCount = 0;
for (const entry of veg.packEntries) {
    const p = entry.path.toLowerCase();
    if (p.endsWith('.rigid_model_v2')) {
        let isTarget = false;
        let outName = '';

        if (p.includes('/shrub_') || p.includes('/fern_') || p.includes('plant_tropical_') ||
            p.includes('pine_q_shrub') || p.includes('pine_r_shrub') ||
            p.includes('tree_scholar_k_bush') || p.includes('tree_scholar_l_bush') || p.includes('tree_scholar_m_bush') ||
            p.includes('tree_shrub_') || p.includes('tree_bamboo_stump') || p.includes('/stump/')) {
            isTarget = true;
            const filename = path.basename(entry.path, '.rigid_model_v2');
            outName = filename.replace(/^tree_/, '').replace(/^shrub_/, 'Shrub_');
            // Capitalize properly
            outName = outName.split('_').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join('_');
        }

        if (isTarget) {
            const buf = Buffer.alloc(entry.size);
            fs.readSync(veg.fd, buf, 0, entry.size, entry.offset);
            try {
                const res = convertRmv2ToObj(buf, outName);
                fs.writeFileSync(path.join(shrubsDir, `${outName}.obj`), res.obj, 'utf8');
                fs.writeFileSync(path.join(shrubsDir, `${outName}.mtl`), res.mtl, 'utf8');
                shrubExtractedCount++;
                console.log(`[Shrub ${shrubExtractedCount}] Extracted ${outName}.obj`);
            } catch (err) {
                console.error(`Error converting ${entry.path}: ${err.message}`);
            }
        }
    } else if (p.endsWith('.dds')) {
        // Extract textures if they are shrub/grass/leaf related
        if (p.includes('shrub') || p.includes('grass') || p.includes('fern') || p.includes('leaf') ||
            p.includes('flower') || p.includes('rice') || p.includes('millet') || p.includes('tea') ||
            p.includes('rhododendron') || p.includes('convallaria') || p.includes('lotus') || p.includes('stump')) {
            const outTex = path.join(texturesDir, path.basename(entry.path));
            if (!fs.existsSync(outTex)) {
                const buf = Buffer.alloc(entry.size);
                fs.readSync(veg.fd, buf, 0, entry.size, entry.offset);
                // Patch DX10 header miscFlags2 to 1 (STRAIGHT ALPHA) if DX10
                if (buf.length > 148 && buf.toString('ascii', 80, 84) === 'DX10') {
                    buf.writeUInt32LE(1, 144);
                }
                fs.writeFileSync(outTex, buf);
            }
        }
    }
}
fs.closeSync(veg.fd);
console.log(`Total Shrubs & Foliage extracted: ${shrubExtractedCount}`);

// 2. Process terrain.pack (Detail Grass: Climate structured)
console.log('--- Processing terrain.pack (Detail Grass) ---');
const terr = readPackIndex(terrainPackPath);

let grassExtractedCount = 0;
for (const entry of terr.packEntries) {
    const p = entry.path.toLowerCase();
    if (p.endsWith('.rigid_model_v2') && p.includes('terrain/vegetation/battle/grass/')) {
        const filename = path.basename(entry.path, '.rigid_model_v2'); // e.g. ard_grass0_1

        // Determine climate folder
        let climateDir = 'Temperate';
        if (filename.startsWith('ard_')) climateDir = 'Arid';
        else if (filename.startsWith('nan_')) climateDir = 'Nanman';
        else if (filename.startsWith('cld_')) climateDir = 'Cold';
        else if (filename.startsWith('sbt_')) climateDir = 'Subtropical';
        else if (filename.startsWith('tmp_')) climateDir = 'Temperate';

        const targetFolder = path.join(grassDir, `Climate_${climateDir}`);
        if (!fs.existsSync(targetFolder)) fs.mkdirSync(targetFolder, { recursive: true });

        const buf = Buffer.alloc(entry.size);
        fs.readSync(terr.fd, buf, 0, entry.size, entry.offset);
        try {
            const outName = filename.split('_').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join('_');
            const res = convertRmv2ToObj(buf, outName);
            fs.writeFileSync(path.join(targetFolder, `${outName}.obj`), res.obj, 'utf8');
            fs.writeFileSync(path.join(targetFolder, `${outName}.mtl`), res.mtl, 'utf8');
            grassExtractedCount++;
            if (grassExtractedCount % 20 === 0 || grassExtractedCount <= 10) {
                console.log(`[Grass ${grassExtractedCount}] Extracted ${climateDir}/${outName}.obj`);
            }
        } catch (err) {
            console.error(`Error converting grass ${entry.path}: ${err.message}`);
        }
    }
}
fs.closeSync(terr.fd);
console.log(`Total Detail Grass extracted: ${grassExtractedCount}`);
console.log('--- ALL VEGETATION & DETAIL GRASS EXTRACTION COMPLETE! ---');
