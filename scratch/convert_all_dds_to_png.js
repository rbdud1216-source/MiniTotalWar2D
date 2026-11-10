const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const rootDir = 'C:\\unityProject\\MiniTotalWar2D\\Assets\\TotalWar_ThreeKingdoms_Extracted';

// 1. 재귀적으로 모든 .dds 파일 탐색
function getDdsFiles(dir, fileList = []) {
    const files = fs.readdirSync(dir);
    for (const f of files) {
        const fullPath = path.join(dir, f);
        const stat = fs.statSync(fullPath);
        if (stat.isDirectory()) {
            getDdsFiles(fullPath, fileList);
        } else if (f.toLowerCase().endsWith('.dds')) {
            fileList.push(fullPath);
        }
    }
    return fileList;
}

console.log('DDS 파일 검색 중...');
const allDds = getDdsFiles(rootDir);
console.log(`총 검색된 DDS 파일 수: ${allDds.length}개`);

// 2. 이미 PNG가 있는 것 제외
const targets = [];
let skipped = 0;

for (const ddsPath of allDds) {
    const pngPath = ddsPath.replace(/\.dds$/i, '.png');
    if (fs.existsSync(pngPath)) {
        skipped++;
    } else {
        targets.push({ ddsPath, pngPath });
    }
}

console.log(`이미 변환된 PNG: ${skipped}개`);
console.log(`새로 변환할 대상: ${targets.length}개`);

if (targets.length === 0) {
    console.log('모든 DDS 파일이 이미 PNG로 변환되어 있습니다.');
    process.exit(0);
}

// 3. DX10 헤더 arraySize 패치 (필요 시)
function ensureDx10Header(filePath) {
    try {
        const buf = fs.readFileSync(filePath);
        if (buf.length > 148 && buf.toString('ascii', 80, 84) === 'DX10') {
            const arraySize = buf.readUInt32LE(144);
            if (arraySize === 0) {
                buf.writeUInt32LE(1, 144);
                fs.writeFileSync(filePath, buf);
            }
        }
    } catch (e) {
        // 무시
    }
}

// 4. 병렬 변환 큐 (동시 8개 작업)
const CONCURRENCY = 8;
let currentIndex = 0;
let successCount = 0;
let failCount = 0;

function convertFile(item) {
    return new Promise((resolve) => {
        ensureDx10Header(item.ddsPath);
        const cmd = `ffmpeg -v error -y -i "${item.ddsPath}" -update 1 -frames:v 1 "${item.pngPath}"`;
        exec(cmd, (err, stdout, stderr) => {
            if (err) {
                failCount++;
                console.error(`[실패] ${path.basename(item.ddsPath)}: ${stderr || err.message}`);
            } else {
                successCount++;
            }
            resolve();
        });
    });
}

async function worker() {
    while (currentIndex < targets.length) {
        const idx = currentIndex++;
        const item = targets[idx];
        await convertFile(item);
        if ((successCount + failCount) % 100 === 0 || (successCount + failCount) === targets.length) {
            console.log(`진행률: ${successCount + failCount} / ${targets.length} (성공: ${successCount}, 실패: ${failCount})`);
        }
    }
}

async function main() {
    const workers = [];
    for (let i = 0; i < CONCURRENCY; i++) {
        workers.push(worker());
    }
    await Promise.all(workers);
    console.log(`\n🎉 모든 변환 완료! 총 성공: ${successCount}개, 실패: ${failCount}개, 기존 유지: ${skipped}개`);
}

main();

