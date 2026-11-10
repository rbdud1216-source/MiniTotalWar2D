const fs = require('fs');

const lines = fs.readFileSync('Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj', 'utf8').split('\n');

const v = [];
const vt = [];
const faces = [];

for (const l of lines) {
    if (l.startsWith('v ')) {
        const p = l.substring(2).trim().split(/\s+/).map(Number);
        v.push(p);
    } else if (l.startsWith('vt ')) {
        const p = l.substring(3).trim().split(/\s+/).map(Number);
        vt.push(p);
    } else if (l.startsWith('f ')) {
        const p = l.substring(2).trim().split(/\s+/).map(s => {
            const parts = s.split('/');
            return {
                v: parseInt(parts[0]) - 1,
                vt: parseInt(parts[1]) - 1
            };
        });
        faces.push(p);
    }
}

console.log(`Tall_D: ${v.length} verts, ${vt.length} uvs, ${faces.length} faces`);

// 각 페이스의 UV 바운딩 박스
let minU = 999, maxU = -999, minV = 999, maxV = -999;
for (const f of faces) {
    for (const pt of f) {
        const uv = vt[pt.vt];
        if (uv[0] < minU) minU = uv[0];
        if (uv[0] > maxU) maxU = uv[0];
        if (uv[1] < minV) minV = uv[1];
        if (uv[1] > maxV) maxV = uv[1];
    }
}

console.log(`UV Bounds: U=[${minU.toFixed(4)} ~ ${maxU.toFixed(4)}], V=[${minV.toFixed(4)} ~ ${maxV.toFixed(4)}]`);

// 개별 페이스들의 UV 중심과 크기
for (let i = 0; i < Math.min(10, faces.length); i++) {
    const f = faces[i];
    const uvs = f.map(p => vt[p.vt]);
    console.log(`Face ${i+1}: UV0=(${uvs[0][0].toFixed(3)}, ${uvs[0][1].toFixed(3)}) UV1=(${uvs[1][0].toFixed(3)}, ${uvs[1][1].toFixed(3)}) UV2=(${uvs[2][0].toFixed(3)}, ${uvs[2][1].toFixed(3)})`);
}

