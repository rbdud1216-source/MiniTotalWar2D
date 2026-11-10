const fs = require('fs');
const { execSync } = require('child_process');

const texDir = 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures';
const list = [
    'shrubs_ferns_wi_base_colour',
    'shrubs_ferns_au_base_colour',
    'shrub_grassclump_wi_base_colour',
    'shrub_grassclump_au_base_colour',
    'shrub_grassclump_sp_base_colour',
    'shrub_tea_base_colour',
    'shrub_tea_normal'
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
        console.log('Exists or not found:', t);
    }
}
