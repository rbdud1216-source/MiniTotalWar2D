const fs = require("fs");
const lines = fs.readFileSync("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/Huangshan_Pine_A.obj", "utf8").split("\n");
let leavesVerts = [];
let trunkVerts = [];
let currentGroup = "";
for (let l of lines) {
    if (l.startsWith("g ")) currentGroup = l.substring(2).trim();
    if (l.startsWith("v ")) {
        let parts = l.trim().split(/\s+/).slice(1).map(Number);
        if (currentGroup === "Leaves") leavesVerts.push(parts);
        else if (currentGroup === "Trunk") trunkVerts.push(parts);
    }
}
function getBounds(verts) {
    let minY = Infinity, maxY = -Infinity;
    for (let v of verts) {
        if (v[1] < minY) minY = v[1];
        if (v[1] > maxY) maxY = v[1];
    }
    return { count: verts.length, minY, maxY, centerY: (minY + maxY) / 2 };
}
console.log("Trunk bounds:", getBounds(trunkVerts));
console.log("Leaves bounds:", getBounds(leavesVerts));
