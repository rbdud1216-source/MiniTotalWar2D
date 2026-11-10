const fs = require('fs');
const { execSync } = require('child_process');

const dir = 'Assets/TotalWar_ThreeKingdoms_Extracted/Environment/Textures';
const list = [
    'ard_grass0_base_colour',
    'ard_rock0_base_colour',
    'ard_stones0_base_colour',
    'ard_stones1_base_colour',
    'cld_grass0_base_colour'
];

for (const t of list) {
    const dds = dir + '/' + t + '.dds';
    const png = dir + '/' + t + '.png';
    if (!fs.existsSync(png)) {
        try {
            execSync(`ffmpeg -y -i "${dds}" "${png}"`, { stdio: 'ignore' });
            console.log('Converted:', t + '.png');
        } catch (e) {
            console.log('Failed:', t, e.message);
        }
    } else {
        console.log('Already exists:', t + '.png');
    }
}
