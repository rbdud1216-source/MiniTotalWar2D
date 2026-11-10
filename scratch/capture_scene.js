const { execSync } = require('child_process');
const fs = require('fs');

try {
    const raw = execSync('powershell -Command "unity cmd capture_scene_view"', {
        maxBuffer: 30 * 1024 * 1024,
        encoding: 'utf8'
    });
    
    // The format is base64 string followed by ","bytes":...
    const endIdx = raw.indexOf('","bytes"');
    if (endIdx > 0) {
        // Find the preceding quote
        const startIdx = raw.lastIndexOf('"', endIdx - 1);
        const b64 = raw.substring(startIdx + 1, endIdx);
        const buf = Buffer.from(b64, 'base64');
        const target = 'C:/Users/PC/.gemini/antigravity-ide/brain/27c3bc13-c555-4e54-8fe3-64dcb683a499/current_scene_view.png';
        fs.writeFileSync(target, buf);
        console.log('Saved image successfully:', target, buf.length, 'bytes');
    } else {
        console.log('Could not find end of base64');
    }
} catch (e) {
    console.error('Error:', e.message);
}
