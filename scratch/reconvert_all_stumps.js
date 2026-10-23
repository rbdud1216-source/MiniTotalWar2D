const fs = require('fs');
const path = require('path');

const packPath = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data\\vegetation.pack';
const outBaseDir = 'C:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted\\Vegetation';
const treesDir = path.join(outBaseDir, 'Trees');
const shrubsDir = path.join(outBaseDir, 'Shrubs');
const texturesDir = path.join(outBaseDir, 'Textures');

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

            // 1. Position 정밀 감지 (pos8 vs pos0)
            let x = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 8));
            let y = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 10));
            let z = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 12));

            // pos8이 [0,0,0]인 경우 pos0(ptr + 0, 2, 4)에서 포지션 획득!
            if (x === 0 && y === 0 && z === 0) {
                const x0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 0));
                const y0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 2));
                const z0 = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 4));
                if (x0 !== 0 || y0 !== 0 || z0 !== 0) {
                    x = x0;
                    y = y0;
                    z = z0;
                }
            }
            verts.push([x, y, z]);

            // 2. Vertex Normal (offset 16, 18, 20)
            let nx = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 16));
            let ny = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 18));
            let nz = decodeFloat16(rmv2Buf.readUInt16LE(ptr + 20));
            if (isNaN(nx) || !isFinite(nx)) nx = 0;
            if (isNaN(ny) || !isFinite(ny)) ny = 1;
            if (isNaN(nz) || !isFinite(nz)) nz = 0;
            normals.push([nx, ny, nz]);

            // 3. Texture UV (offset 40, 42) & V축 반전!
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

    // Trunk 서브메시가 항상 먼저 오도록 정렬 (Submesh 0 = Trunk, Submesh 1 = Leaves, Submesh 2 = Twigs)
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
        objLines.push(`g ${groupName}`);
        objLines.push(`usemtl ${groupName}_Mat`);

        // Vertices
        for (const [x, y, z] of sm.verts) {
            objLines.push(`v ${x.toFixed(4)} ${y.toFixed(4)} ${z.toFixed(4)}`);
        }

        // UVs
        for (const [u, v] of sm.uvs) {
            objLines.push(`vt ${u.toFixed(4)} ${v.toFixed(4)}`);
        }

        // Normals
        for (const [nx, ny, nz] of sm.normals) {
            objLines.push(`vn ${nx.toFixed(4)} ${ny.toFixed(4)} ${nz.toFixed(4)}`);
        }

        // Faces (1-based global index)
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

// 대상 36종 모델 전체 재변환 (특히 Stump_A~E 및 Bamboo_Stump)
const modelsToConvert = [
    // 1. 소나무 고사목
    { path: 'battleterrain/vegetation/trees/pine/pine_m_dead.rigid_model_v2', outName: 'Pine_Dead_M', isTree: true },
    { path: 'battleterrain/vegetation/trees/pine/pine_o_dead.rigid_model_v2', outName: 'Pine_Dead_O', isTree: true },
    { path: 'battleterrain/vegetation/trees/pine/pine_s_dead.rigid_model_v2', outName: 'Pine_Dead_S', isTree: true },
    // 2. 부러진 소나무
    { path: 'battleterrain/vegetation/trees/pine/pine_h_broken.rigid_model_v2', outName: 'Pine_Broken_H', isTree: true },
    { path: 'battleterrain/vegetation/trees/pine/pine_k_broken.rigid_model_v2', outName: 'Pine_Broken_K', isTree: true },
    { path: 'battleterrain/vegetation/trees/pine/pine_l_broken.rigid_model_v2', outName: 'Pine_Broken_L', isTree: true },
    { path: 'battleterrain/vegetation/trees/pine/pine_n_broken.rigid_model_v2', outName: 'Pine_Broken_N', isTree: true },
    // 3. 부러진 회화나무
    { path: 'battleterrain/vegetation/trees/scholar/tree_scholar_broken_a.rigid_model_v2', outName: 'Scholar_Broken_A', isTree: true },
    { path: 'battleterrain/vegetation/trees/scholar/tree_scholar_broken_b.rigid_model_v2', outName: 'Scholar_Broken_B', isTree: true },
    { path: 'battleterrain/vegetation/trees/scholar/tree_scholar_broken_c.rigid_model_v2', outName: 'Scholar_Broken_C', isTree: true },
    { path: 'battleterrain/vegetation/trees/scholar/tree_scholar_broken_d.rigid_model_v2', outName: 'Scholar_Broken_D', isTree: true },
    // 4. 구실잣밤 고사목
    { path: 'battleterrain/vegetation/trees/castanopsis/castanopsis_a_dead.rigid_model_v2', outName: 'Castanopsis_Dead_A', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis/castanopsis_b_dead.rigid_model_v2', outName: 'Castanopsis_Dead_B', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis/castanopsis_c_dead.rigid_model_v2', outName: 'Castanopsis_Dead_C', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis/castanopsis_d_dead.rigid_model_v2', outName: 'Castanopsis_Dead_D', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis/castanopsis_e_dead.rigid_model_v2', outName: 'Castanopsis_Dead_E', isTree: true },
    // 5. 남만 정글 고사목
    { path: 'battleterrain/vegetation/trees/castanopsis_jugle/castanopsis_jugle_dead_a.rigid_model_v2', outName: 'Castanopsis_Jungle_Dead_A', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis_jugle/castanopsis_jugle_dead_b.rigid_model_v2', outName: 'Castanopsis_Jungle_Dead_B', isTree: true },
    { path: 'battleterrain/vegetation/trees/castanopsis_jugle/castanopsis_jugle_dead_c.rigid_model_v2', outName: 'Castanopsis_Jungle_Dead_C', isTree: true },
    // 6. 대나무 밑동 (단일)
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_a.rigid_model_v2', outName: 'Bamboo_Stump_A', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_b.rigid_model_v2', outName: 'Bamboo_Stump_B', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_c.rigid_model_v2', outName: 'Bamboo_Stump_C', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_d.rigid_model_v2', outName: 'Bamboo_Stump_D', isTree: false },
    // 7. 대나무 밑동 군락 (Group)
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_group_a.rigid_model_v2', outName: 'Bamboo_Stump_Group_A', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_group_b.rigid_model_v2', outName: 'Bamboo_Stump_Group_B', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_group_c.rigid_model_v2', outName: 'Bamboo_Stump_Group_C', isTree: false },
    { path: 'battleterrain/vegetation/trees/bamboo/tree_bamboo_stump_group_d.rigid_model_v2', outName: 'Bamboo_Stump_Group_D', isTree: false },
    // 8. 거목 그루터기
    { path: 'battleterrain/vegetation/trees/stump/stump_a.rigid_model_v2', outName: 'Stump_A', isTree: false },
    { path: 'battleterrain/vegetation/trees/stump/stump_b.rigid_model_v2', outName: 'Stump_B', isTree: false },
    { path: 'battleterrain/vegetation/trees/stump/stump_c.rigid_model_v2', outName: 'Stump_C', isTree: false },
    { path: 'battleterrain/vegetation/trees/stump/stump_d.rigid_model_v2', outName: 'Stump_D', isTree: false },
    { path: 'battleterrain/vegetation/trees/stump/stump_e.rigid_model_v2', outName: 'Stump_E', isTree: false },
    // 9. 부러진 나뭇가지 더미
    { path: 'battleterrain/vegetation/trees/shrub_branch/tree_shrub_brokenbranch_a.rigid_model_v2', outName: 'BrokenBranch_A', isTree: false },
    { path: 'battleterrain/vegetation/trees/shrub_branch/tree_shrub_brokenbranch_b.rigid_model_v2', outName: 'BrokenBranch_B', isTree: false },
    { path: 'battleterrain/vegetation/trees/shrub_branch/tree_shrub_brokenbranch_c.rigid_model_v2', outName: 'BrokenBranch_C', isTree: false },
    { path: 'battleterrain/vegetation/trees/shrub_branch/tree_shrub_brokenbranch_d.rigid_model_v2', outName: 'BrokenBranch_D', isTree: false }
];

for (const m of modelsToConvert) {
    const entry = packEntries.find(e => e.path === m.path);
    if (!entry) {
        console.error(`Not found in pack: ${m.path}`);
        continue;
    }
    const buf = Buffer.alloc(entry.size);
    fs.readSync(fd, buf, 0, entry.size, entry.offset);

    const result = convertRmv2ToObj(buf, m.outName);
    const targetDir = m.isTree ? treesDir : shrubsDir;
    fs.writeFileSync(path.join(targetDir, `${m.outName}.obj`), result.obj, 'utf8');
    fs.writeFileSync(path.join(targetDir, `${m.outName}.mtl`), result.mtl, 'utf8');
    console.log(`Successfully reconverted: ${m.outName}.obj (${result.obj.length} bytes)`);
}

console.log('All 36 models successfully reconverted!');
