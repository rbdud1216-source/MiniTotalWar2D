const fs = require('fs');
const { execSync } = require('child_process');

const texDir = 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures';
const targets = [
    'shrub_flower_a_billboard_base_colour',
    'shrub_flower_a_billboard_normal',
    'shrub_elephantgrass_a_billboard_base_colour',
    'shrub_elephantgrass_a_billboard_normal',
    'sbt_grass_base_colour',
    'plant_tropical_bigleaf_a_billboard_base_colour',
    'plant_tropical_bigleaf_a_billboard_normal'
];

for (const t of targets) {
    const dds = `${texDir}/${t}.dds`;
    const png = `${texDir}/${t}.png`;
    if (fs.existsSync(dds) && !fs.existsSync(png)) {
        try {
            execSync(`ffmpeg -y -i "${dds}" "${png}"`, { stdio: 'ignore' });
            console.log('변환 완료:', t + '.png');
        } catch (e) {
            console.error('변환 실패:', t, e.message);
        }
    } else {
        console.log('이미 존재하거나 없음:', t);
    }
}
