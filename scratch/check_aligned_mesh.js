const fs = require('fs');
const text = fs.readFileSync('Assets/Settings/Vegetation/RadialMeshes/Sbt_Grass0_1_shrub_grassclump_su_base_colour_AlignedMesh.asset', 'utf8');
console.log(text.slice(0, 400));
