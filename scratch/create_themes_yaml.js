const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

function randomGuid() {
    return crypto.randomBytes(16).toString('hex');
}

function getGuid(metaPath) {
    if (!fs.existsSync(metaPath)) return null;
    const content = fs.readFileSync(metaPath, 'utf8');
    const match = content.match(/guid:\s*([a-f0-9]{32})/);
    return match ? match[1] : null;
}

const themeScriptGuid = '34f80ae7da82fbc42a6ba484363651f8';
const layerDir = 'Assets/TerrainSampleAssets/TerrainLayers';
const grassDir = 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass';
const outDir = 'Assets/Settings/TerrainThemes';

if (!fs.existsSync('Assets/Settings')) fs.mkdirSync('Assets/Settings');
if (!fs.existsSync(outDir)) fs.mkdirSync(outDir);

function getLayerRef(layerName) {
    if (!layerName) return '{fileID: 0}';
    const guid = getGuid(path.join(layerDir, `${layerName}.terrainlayer.meta`));
    if (!guid) return '{fileID: 0}';
    return `{fileID: 11400000, guid: ${guid}, type: 2}`;
}

function getGrassConfigs(subFolder) {
    const folderPath = path.join(grassDir, subFolder);
    if (!fs.existsSync(folderPath)) return [];
    const files = fs.readdirSync(folderPath).filter(f => f.endsWith('.obj'));
    const configs = [];
    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const metaPath = path.join(folderPath, `${file}.meta`);
        const guid = getGuid(metaPath);
        if (guid) {
            configs.push({
                guid,
                name: file.replace('.obj', '')
            });
        }
    }
    return configs;
}

const themesDef = [
    {
        name: 'Temperate_Theme',
        climate: 0, // Temperate
        suLayer: 'Grass_A_TerrainLayer',
        auLayer: 'Grass_Dry_TerrainLayer',
        wiLayer: 'Snow_TerrainLayer',
        forestLayer: 'Grass_B_TerrainLayer',
        cliffLayer: 'Rock_TerrainLayer',
        dirtLayer: 'Pebbles_A_TerrainLayer',
        grassFolder: 'Climate_Temperate'
    },
    {
        name: 'Nanman_Theme',
        climate: 1, // Nanman
        suLayer: 'Grass_Moss_TerrainLayer',
        auLayer: 'Muddy_TerrainLayer',
        wiLayer: 'Grass_Soil_TerrainLayer',
        forestLayer: 'Muddy_TerrainLayer',
        cliffLayer: 'Rock_TerrainLayer',
        dirtLayer: 'Soil_Rocks_TerrainLayer',
        grassFolder: 'Climate_Subtropical'
    },
    {
        name: 'Arid_Theme',
        climate: 2, // Arid
        suLayer: 'Grass_Dry_TerrainLayer',
        auLayer: 'Sand_TerrainLayer',
        wiLayer: 'Black_Sand_TerrainLayer',
        forestLayer: 'Sand_TerrainLayer',
        cliffLayer: 'Rock_TerrainLayer',
        dirtLayer: 'Pebbles_C_TerrainLayer',
        grassFolder: 'Climate_Arid'
    },
    {
        name: 'Cold_Theme',
        climate: 4, // Cold
        suLayer: 'Grass_Dry_TerrainLayer',
        auLayer: 'Heather_TerrainLayer',
        wiLayer: 'Snow_TerrainLayer',
        forestLayer: 'Soil_Rocks_TerrainLayer',
        cliffLayer: 'Rock_TerrainLayer',
        dirtLayer: 'Pebbles_B_TerrainLayer',
        grassFolder: 'Climate_Cold'
    },
    {
        name: 'Subtropical_Theme',
        climate: 3, // Subtropical
        suLayer: 'Grass_A_TerrainLayer',
        auLayer: 'Heather_TerrainLayer',
        wiLayer: 'Tidal_Pools_TerrainLayer',
        forestLayer: 'Grass_Moss_TerrainLayer',
        cliffLayer: 'Rock_TerrainLayer',
        dirtLayer: 'Muddy_TerrainLayer',
        grassFolder: 'Climate_Subtropical'
    }
];

for (const t of themesDef) {
    const assetPath = path.join(outDir, `${t.name}.asset`);
    const metaPath = path.join(outDir, `${t.name}.asset.meta`);

    const grassConfigs = getGrassConfigs(t.grassFolder, 15);

    let grassYaml = '';
    if (grassConfigs.length === 0) {
        grassYaml = '  detailGrassConfigs: []\n';
    } else {
        grassYaml = '  detailGrassConfigs:\n';
        for (const gc of grassConfigs) {
            grassYaml += `  - grassPrefab: {fileID: 100000, guid: ${gc.guid}, type: 3}\n`;
            grassYaml += `    dryGrassPrefab: {fileID: 0}\n`;
            grassYaml += `    densityFactor: 1\n`;
            grassYaml += `    widthRange: {x: 0.85, y: 1.35}\n`;
            grassYaml += `    heightRange: {x: 0.8, y: 1.4}\n`;
            grassYaml += `    alignToGround: 0.5\n`;
        }
    }

    const yaml = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${themeScriptGuid}, type: 3}
  m_Name: ${t.name}
  m_EditorClassIdentifier: 
  climate: ${t.climate}
  summerGrassLayer: ${getLayerRef(t.suLayer)}
  autumnGrassLayer: ${getLayerRef(t.auLayer)}
  winterGrassLayer: ${getLayerRef(t.wiLayer)}
  forestFloorLayer: ${getLayerRef(t.forestLayer)}
  cliffRockLayer: ${getLayerRef(t.cliffLayer)}
  dirtRoadLayer: ${getLayerRef(t.dirtLayer)}
${grassYaml}  detailDistance: 120
  detailDensity: 0.75
`;

    fs.writeFileSync(assetPath, yaml, 'utf8');

    if (!fs.existsSync(metaPath)) {
        const meta = `fileFormatVersion: 2
guid: ${randomGuid()}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
        fs.writeFileSync(metaPath, meta, 'utf8');
    }

    console.log(`Created theme asset: ${t.name}.asset (${grassConfigs.length} grass prototypes)`);
}

// Meta for TerrainThemes folder
const folderMeta = path.join(outDir + '.meta');
if (!fs.existsSync(folderMeta)) {
    fs.writeFileSync(folderMeta, `fileFormatVersion: 2\nguid: ${randomGuid()}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

console.log('All 5 themes successfully created!');
