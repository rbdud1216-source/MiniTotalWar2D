const { execSync } = require('child_process');
const fs = require('fs');

const texs = [
    'shrub_magnolia_sp_base_colour',
    'shrub_rhododendron_sp_base_colour',
    'shrub_camelia_br_base_colour',
    'shrub_grassclump_sp_base_colour',
    'shrub_grassclump_su_base_colour'
];

for (let t of texs) {
    const dds = 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/' + t + '.dds';
    if (!fs.existsSync(dds)) {
        console.log(t, 'file not found');
        continue;
    }
    try {
        const cmd = `ffmpeg -v error -i "${dds}" -vf scale=256:256 -f image2pipe -vcodec ppm -`;
        const ppm = execSync(cmd, { maxBuffer: 10 * 1024 * 1024 });
        const headerEnd = ppm.indexOf(0x0a, ppm.indexOf(0x0a, 3) + 1) + 1;
        const data = ppm.slice(headerEnd);
        let yellow = 0, pinkOrRed = 0, white = 0;
        for (let i = 0; i < data.length; i += 3) {
            const r = data[i], g = data[i+1], b = data[i+2];
            if (r > 160 && g > 140 && b < 100) yellow++;
            else if (r > 160 && (g < 100 || b > 120)) pinkOrRed++;
            else if (r > 180 && g > 180 && b > 180) white++;
        }
        console.log(t, '-> Yellow:', yellow, 'Pink/Red:', pinkOrRed, 'White:', white);
    } catch (e) {
        console.log(t, 'error:', e.message);
    }
}
