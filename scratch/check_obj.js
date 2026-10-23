const fs = require("fs");
const lines = fs.readFileSync("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/Huangshan_Pine_A.obj", "utf8").split("\n");
let objects = [];
let usemtls = [];
for (let l of lines) {
    if (l.startsWith("o ") || l.startsWith("g ")) objects.push(l.trim());
    if (l.startsWith("usemtl ")) usemtls.push(l.trim());
}
console.log("Objects/Groups:", objects.slice(0, 20));
console.log("Materials:", usemtls);
