const fs = require('fs');

const packPath = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Total War THREE KINGDOMS\\data\\vegetation.pack';
const fd = fs.openSync(packPath, 'r');

const header = Buffer.alloc(16);
fs.readSync(fd, header, 0, 16, 0);

const meta = Buffer.alloc(12);
fs.readSync(fd, meta, 0, 12, 16);
const fileCount = meta.readInt32LE(0);
const indexSize = meta.readInt32LE(4);

console.log(`Pack file count: ${fileCount}, index size: ${indexSize}`);

const indexBuffer = Buffer.alloc(indexSize);
fs.readSync(fd, indexBuffer, 0, indexSize, 28);

let pos = 0;
const results = [];
for (let i = 0; i < fileCount; i++) {
    const size = indexBuffer.readInt32LE(pos);
    pos += 4;
    const comp = indexBuffer.readUInt8(pos);
    pos += 1;
    let strEnd = pos;
    while (indexBuffer[strEnd] !== 0) strEnd++;
    const path = indexBuffer.toString('utf8', pos, strEnd);
    pos = strEnd + 1;

    const lower = path.toLowerCase();
    if (lower.includes('snow') || lower.includes('_wi_') || lower.includes('winter') || lower.includes('frost')) {
        results.push({ path, size, comp });
    }
}

fs.closeSync(fd);

console.log(`Found ${results.length} snow/winter entries:`);
results.slice(0, 50).forEach(r => console.log(`  ${r.path} (${r.size} bytes)`));
