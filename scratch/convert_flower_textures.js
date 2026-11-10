const fs = require('fs');
const { execSync } = require('child_process');

const texDir = 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures';
const list = [
    'shrub_magnolia_sp_base_colour',
    'shrub_magnolia_normal',
    'shrub_rhododendron_sp_base_colour',
    'shrub_rhododendron_normal'
];

for (const t of list) {
    const dds = texDir + '/' + t + '.dds';
    const png = texDir + '/' + t + '.png';
    if (fs.existsSync(dds) && !fs.existsSync(png)) {
        try {
            execSync('ffmpeg -y -i "' + dds + '" "' + png + '"', { stdio: 'ignore' });
            console.log('Converted:', t + '.png');
        } catch (e) {
            console.error('Failed:', t, e.message);
        }
    } else {
        console.log('Skipped or already exists:', t);
    }
}
