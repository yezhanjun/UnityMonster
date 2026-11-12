// Read-only source inventory. Writes only Docs/Resources outputs.
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '..');
const out = path.join(root, 'Docs', 'Resources');
fs.mkdirSync(out, {recursive:true});
const git = (...args) => cp.execFileSync('git', ['-C',root,...args], {encoding:'utf8',maxBuffer:32*1024*1024});
const tracked = git('ls-files','-z').split('\0').filter(Boolean);
const commit = git('rev-parse','HEAD').trim();
const lfs = JSON.parse(git('lfs','ls-files','--json')).files;
const lfsMap = new Map(lfs.map(x=>[x.name,x]));
const unit = n => (n/1024/1024).toFixed(2)+' MiB';
const gib = n => (n/1024/1024/1024).toFixed(2)+' GiB';
const escape = s => String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const md = s => String(s).replaceAll('|','\\|').replaceAll('\n',' ');
const role = p => {
 if(p.startsWith('Packages/')) return '包与插件';
 if(p.startsWith('ProjectSettings/')) return '工程设置';
 if(p.startsWith('Assets/Monsters/Jotunnslayer/Enemies/')) return p.includes('/Editor/')?'怪物编辑器工具':'巨人杀手怪物';
 if(p.startsWith('Assets/Resources/RainActors/')) return '雨中冒险角色与怪物';
 if(p.startsWith('Assets/Monsters/Jotunnslayer/')) return '巨人杀手配套';
 if(p.startsWith('Assets/Game/Baseline/')) return p.includes('/Editor/')?'验收编辑器工具':'运行入口与战斗适配';
 if(p.startsWith('Assets/ThirdParty/Mirror/')||p.startsWith('Assets/ThirdParty/')) return '第三方基础组件';
 if(p.startsWith('Assets/Game/Maps/')) return '场景与场景配套';
 if(p.startsWith('Assets/Resources/RainImported/')) return '雨中冒险图鉴音效与配置';
 if(p.startsWith('Assets/Resources/DemonHunter3C/')||p.startsWith('Assets/Game/CharacterSelection/')||p.startsWith('Assets/VRC/Characters/')||p.startsWith('Assets/Resources/PortableCharacters/')||p.startsWith('Assets/Resources/SuperBotPackVol1/'))return '玩家角色与角色配套';
 if(p.startsWith('Assets/Resources/')) return '共享运行资源';
 if(p.startsWith('Assets/AssetStore/AaaWeapons/')||p.startsWith('Assets/AssetStore/KINEMATION/')) return '武器与动作框架';
 if(p.startsWith('Assets/AssetStore/desNetware/')||p.startsWith('Assets/AssetStore/Demo/')||p.startsWith('Assets/AssetStore/TirgamesAssets/')) return '继承的框架与演示配套';
 if(p.startsWith('Assets/Game/Localization/')||p.startsWith('Assets/TextMesh Pro/')||p.startsWith('Assets/AssetStore/DamageNumbers/')) return '界面字体与反馈';
 return '其他角色材质与共享配套';
};
function type(p) {
 const ext=path.extname(p).toLowerCase();
 if(ext==='.asset') {
  const fd=fs.openSync(path.join(root,p),'r'), b=Buffer.alloc(2048);
  const n=fs.readSync(fd,b,0,b.length,0);fs.closeSync(fd);
  const id=/--- !u!(\d+)/.exec(b.subarray(0,n).toString('utf8'))?.[1];
  return ({43:'网格',90:'Avatar',28:'贴图',74:'动画',21:'材质',83:'音频',114:'ScriptableObject或配置',156:'地形数据',48:'Shader'})[id]||'Unity 序列化资源';
 }
 if(['.fbx','.obj','.dae','.blend'].includes(ext))return '模型文件';
 if(ext==='.prefab')return 'Prefab';
 if(['.anim','.controller','.overridecontroller','.mask'].includes(ext))return '动画';
 if(ext==='.mat')return '材质';
 if(['.png','.jpg','.jpeg','.tga','.dds','.psd','.exr','.hdr','.rendertexture','.cubemap','.tif','.tiff','.bmp','.gif','.iff'].includes(ext))return '贴图';
 if(['.wav','.ogg','.mp3','.aiff','.aif'].includes(ext))return '音频';
 if(ext==='.mixer')return '音频配置';
 if(ext==='.physicmaterial')return '物理材质';
 if(ext==='.lighting')return '烘焙光照配置';
 if(ext==='.unity')return '场景';
 if(['.cs','.asmdef','.asmref','.rsp'].includes(ext))return '脚本与程序集';
 if(['.shader','.shadergraph','.shadersubgraph','.hlsl','.cginc','.compute','.glslinc','.lilblock','.lilinternal','.shadervariants'].includes(ext))return 'Shader';
 if(['.ttf','.otf','.fontsettings'].includes(ext))return '字体';
 if(['.dll','.so','.dylib','.bundle'].includes(ext))return '二进制插件';
 if(['.json','.xml','.txt','.csv','.tsv','.yaml','.yml','.uxml','.uss','.md','.plist','.po','.inputactions'].includes(ext))return '配置与文本';
 return '其他';
}
const files=[], metas=[], guidPaths=new Map();
for(const p of tracked) {
 if(!/^(Assets|Packages|ProjectSettings)\//.test(p))continue;
 const full=path.join(root,p); if(!fs.existsSync(full)||!fs.statSync(full).isFile())continue;
 if(p.endsWith('.meta')) {
  const guid=/^guid: ([a-f0-9]{32})/m.exec(fs.readFileSync(full,'utf8'))?.[1]||'';
  const target=p.slice(0,-5);metas.push({path:p,target,guid,bytes:fs.statSync(full).size});
  if(guid){if(!guidPaths.has(guid))guidPaths.set(guid,[]);guidPaths.get(guid).push(target);}continue;
 }
 const metaPath=p+'.meta', metaFull=path.join(root,metaPath);
 const guid=fs.existsSync(metaFull)?(/^guid: ([a-f0-9]{32})/m.exec(fs.readFileSync(metaFull,'utf8'))?.[1]||''):'';
 const key=p.includes('/Resources/')?p.split('/Resources/').slice(1).join('/Resources/').replace(/\.[^/.]+$/,''):'';
 const lf=lfsMap.get(p);
 files.push({path:p,category:role(p),type:type(p),extension:path.extname(p).toLowerCase(),bytes:fs.statSync(full).size,
  guid,metaPath:fs.existsSync(metaFull)?metaPath:'',resourcesKey:key,lfs:!!lf,oid:lf?.oid||''});
}
const sum=a=>a.reduce((s,x)=>s+x.bytes,0);
function groups(list, key) {
 const map=new Map();for(const x of list){const k=key(x);if(!map.has(k))map.set(k,[]);map.get(k).push(x);}
 return [...map].map(([name,a])=>({name,count:a.length,bytes:sum(a),files:a})).sort((a,b)=>b.bytes-a.bytes);
}
const assets=files.filter(x=>x.path.startsWith('Assets/'));
const folders=groups(assets,x=>x.path.split('/').slice(0,2).join('/'));
const categories=groups(files,x=>x.category),types=groups(assets,x=>x.type);
const duplicates=groups(files.filter(x=>x.oid),x=>x.oid).filter(x=>x.count>1).map(x=>({oid:x.name,bytesPerCopy:x.files[0].bytes,
 potentialBytes:x.files[0].bytes*(x.count-1),paths:x.files.map(f=>f.path)})).sort((a,b)=>b.potentialBytes-a.potentialBytes);
const resources=groups(assets.filter(x=>x.resourcesKey),x=>x.resourcesKey).filter(x=>x.count>1)
 .map(x=>({key:x.name,sameType:new Set(x.files.map(f=>f.type)).size<x.count,paths:x.files.map(f=>({path:f.path,type:f.type}))}));
const audits={missingMeta:assets.filter(x=>!x.metaPath).map(x=>x.path),
 orphanMeta:metas.filter(x=>!fs.existsSync(path.join(root,x.target))).map(x=>x.path),
 duplicateGuids:[...guidPaths].filter(([g,ps])=>ps.length>1).map(([guid,paths])=>({guid,paths})),
 duplicates,resourcesKeyCandidates:resources};
const actorGroup = p => {
 let m=/^Assets\/Jotunnslayer\/Enemies\/Resources\/Jotunnslayer\/([^/]+)\//.exec(p);
 if(m)return '巨人杀手 / '+m[1];
 m=/^Assets\/Resources\/RainActors\/PendingWhitebox\/([^/]+)\//.exec(p);
 if(m)return '雨中冒险 / PendingWhitebox / '+m[1];
 m=/^Assets\/Resources\/RainActors\/RoR2\/(Base|DLC1)\/(?:Characters\/)?([^/]+)\//.exec(p);
 if(m)return '雨中冒险 / '+m[1]+' / '+m[2];
 return '';
};
const actors=groups(assets.filter(x=>actorGroup(x.path)),x=>actorGroup(x.path)).map(g=>({name:g.name,count:g.count,bytes:g.bytes,
 path:g.files.map(f=>f.path).sort()[0].split('/').slice(0, g.name.startsWith('巨人杀手')?6:g.name.includes('PendingWhitebox')?5:g.files[0].path.includes('/Characters/')?7:6).join('/'),
 types:Object.fromEntries(groups(g.files,x=>x.type).map(t=>[t.name,t.count]))}));
const acceptance=JSON.parse(fs.readFileSync(path.join(root,'Docs/Acceptance/CURRENT-PROGRESS.json'),'utf8').replace(/^\uFEFF/,''));
const catalog=JSON.parse(fs.readFileSync(path.join(root,'Docs/Acceptance/怪物截图来源清单.json'),'utf8').replace(/^\uFEFF/,''));
const manifest=JSON.parse(fs.readFileSync(path.join(root,'Packages/manifest.json'),'utf8').replace(/^\uFEFF/,''));
const packageDirs=fs.readdirSync(path.join(root,'Packages'),{withFileTypes:true}).filter(x=>x.isDirectory()).map(x=>x.name);
const result={generatedAt:new Date().toISOString(),sourceCommit:commit,scope:'Git tracked Assets, Packages and ProjectSettings; excludes .meta from resource totals and lists them separately',
 totals:{files:files.length,bytes:sum(files),assetFiles:assets.length,assetBytes:sum(assets),metaFiles:metas.length,metaBytes:sum(metas),
  lfsFiles:files.filter(x=>x.lfs).length,resourcesFiles:assets.filter(x=>x.resourcesKey).length},
 categories:categories.map(({files,...g})=>g),folders:folders.map(({files,...g})=>g),types:types.map(({files,...g})=>g),actors,audits,
 acceptance,registryPackages:manifest.dependencies,embeddedPackages:packageDirs,monsterCatalog:catalog.map(({images,...x})=>({...x,screenshotCount:images.length})),files};
fs.writeFileSync(path.join(out,'资源完整清单.json'),JSON.stringify(result,null,2)+'\n');
const table=(headers,rows)=>'| '+headers.join(' | ')+' |\n| '+headers.map(()=>'---').join(' | ')+' |\n'+rows.map(r=>'| '+r.map(md).join(' | ')+' |').join('\n')+'\n';
const purposes={
 'Assets/Game/CharacterSelection':'角色包、武器动作及玩家外观配套；体积大不等于可删。先核对当前玩家 Prefab 与 Resources 加载。',
 'Assets/Monsters/Jotunnslayer':'怪物本体、动画、材质、图标与导入验收工具；优先保留。',
 'Assets/Resources':'雨中冒险模型、音频、图鉴配置及共享玩家资源；含动态加载，不能按场景引用直接删。',
 'Assets/Game/Baseline':'启动 UI、GM 图鉴、怪物行为与战斗适配、URP 配置；混有运行代码与 Editor 工具。',
 'Assets/ThirdParty/Mirror':'网络与 Host 基础框架；同时保留了 Examples 的配套内容。',
 'Assets/Game/Maps':'白盒场景与其他场景依赖；仅 Startup 和 TrainingWhitebox 进入构建列表。',
 'Assets/AssetStore/desNetware':'MTPSKIT 原框架、玩家与关卡配套；白盒入口仍依赖其中的服务与脚本。',
 'Assets/AssetStore/Demo':'继承的演示与动作资源；其中部分动画被现有角色引用，需逐项核对。',
 'Assets/AssetStore/TirgamesAssets':'继承的模型材质和演示配套，需通过依赖检查决定裁剪范围。',
 'Assets/Game/Localization':'中文字体等界面资源。',
 'Assets/AssetStore/AaaWeapons':'武器与 FPS 框架配套。',
 'Assets/AssetStore/KINEMATION':'动作与武器框架。',
 'Assets/TextMesh Pro':'字体与文字渲染配套。'};
let doc='# UnityMonster 资源清单\n\n'+
 '本工程将巨人杀手怪物、雨中冒险角色、玩家角色包及 MTPSKIT 运行依赖放在了多个资源目录中。优先保留怪物与白盒运行资源，先核对大体积角色包、演示内容和重复文件，再决定搬迁或删除。目录名称本身不能证明资源未使用。\n\n'+
 `扫描基线：\`${commit}\`；生成时间：${result.generatedAt}。统计范围是 Git 跟踪的 Assets、Packages、ProjectSettings，读取本地真实资源大小，LFS 文件按展开后的内容计量。Library、Temp、日志、构建产物及 Docs 不计入资源体积。\n\n`+
 '## 清单入口\n\n'+
 '- [可搜索资源清单](资源清单.html)：按分类、类型、目录和名称筛选，查看路径、GUID、Resources 加载键和 LFS 状态。\n'+
 '- [完整机器清单](资源完整清单.json)：逐文件记录、重复候选、meta 检查、包版本及 283 项怪物验收索引。\n'+
 '- [怪物实机截图与验收表](../Acceptance/怪物实机截图验收汇总.html)：辨别怪物外观和已记录验收情况。\n\n'+
 '## 资源总量\n\n'+table(['项目','数量或体积'],[
 ['Assets 资源文件',assets.length],['Assets 资源体积',gib(sum(assets))],['工程源文件总数',files.length],['工程源文件总量',gib(sum(files))],
 ['meta 文件',metas.length+' 个，'+unit(sum(metas))],['LFS 管理的文件',files.filter(x=>x.lfs).length],['Resources 动态加载目录内文件',assets.filter(x=>x.resourcesKey).length]])+
 '\n文件数不包含 meta；场景、脚本、配置均计作源文件。资源组数量不等于怪物数量，同一个模型可能承载多个怪物配置。\n\n'+
 '## 顶层目录与用途\n\n'+table(['目录','资源文件','体积','用途及处理建议'],folders.map(g=>[g.name,g.count,unit(g.bytes),purposes[g.name]||'角色外观、材质或共享配套，具体内容按完整清单核对；未判定为冗余。']))+
 '\n## 当前资源混杂的主要位置\n\n'+
 `CharacterSelection 占 Assets 体积的 ${(100*folders.find(x=>x.name==='Assets/Game/CharacterSelection').bytes/sum(assets)).toFixed(1)}%，包含玩家角色包及动作资源。它是优先核对的体积来源。LFS 重复候选即使全部可合并，理论也只节省 ${unit(duplicates.reduce((s,x)=>s+x.potentialBytes,0))}，单纯去重不能显著缩小工程。\n\n`+
 '巨人杀手资源集中在 Jotunnslayer，雨中冒险本体在 RainActors，音效与图鉴又分布到 RainImported；运行适配和 GM 在 Baseline。玩家外观和动作还散布在 CharacterSelection、Characters、多个作者目录及 Resources。Editor 导入工具与运行代码混在同一业务目录的不同层级，继承的 Demo、Mirror Examples 和关卡配套仍在仓库。\n\n'+
 '建议先统一分类入口，再逐批核对玩家包、演示配套和旧导入产物。直接把目录名称中含 Demo、Pending 或 Examples 的资源删除，会遗漏被角色和运行工具引用的内容。\n\n'+
 '## 按资源类型统计\n\n'+table(['类型','文件数','体积'],types.map(g=>[g.name,g.count,unit(g.bytes)]))+
 '\nUnity 的 .asset 按 YAML 首个对象类型细分网格、Avatar 等；多对象资源按主文件统计，未把 FBX 内嵌网格、动画拆算为多个文件。\n\n'+
 '## 怪物和角色资源组\n\n'+
 '以下是资源文件夹分组，雨中冒险部分也含幸存者和无人机。表中存在模型或动画只表示资源在库，不表示已经实机通过。PendingWhitebox 是沿用的文件夹名称，其中五组已有历史验收记录，不能按目录名判断失败。\n\n'+
 table(['资源组','文件数','网格或模型','Prefab','动画','材质','贴图','音频','体积'],actors.map(g=>[g.name,g.count,(g.types['网格']||0)+(g.types['模型文件']||0),g.types.Prefab||0,g.types['动画']||0,g.types['材质']||0,g.types['贴图']||0,g.types['音频']||0,unit(g.bytes)]))+
 '\n### 怪物资源的代码入口\n\n'+table(['职责','路径或位置'],[
 ['巨人杀手怪物资源','Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer'],['巨人杀手行为与召唤','Assets/Monsters/Jotunnslayer/Enemies/Skeleton1HSpawn.cs；Skeleton1HCombat.cs；Skeleton1HVisual.cs'],
 ['雨中冒险资源','Assets/Resources/RainActors/RoR2；Assets/Resources/RainActors/PendingWhitebox'],['雨中冒险材质动画适配','Assets/Game/Baseline/RainActorVisual.cs'],
 ['怪物行为与适配','Assets/Game/Baseline/RangeResearchEnemy*.cs；RangeResearchLab.cs'],['GM 图鉴与优先排序','Assets/Game/Baseline/RainImported/JournalBrowser.cs'],
 ['图鉴图标音效配置','Assets/Resources/RainImported'],['导入和动画烘焙工具','Assets/Monsters/Jotunnslayer/Enemies/Editor'],['已有验收证据','Docs/Acceptance；Docs/Validation']])+
 '\n### 近期修复资源的定位\n\n'+table(['怪物','资源目录','记录状态与遗留项'],[
 ['迷你菇','Assets/Resources/RainActors/PendingWhitebox/minimushroom','已记录通过；动画曲线已烘焙。'],
 ['黄铜装置','Assets/Resources/RainActors/PendingWhitebox/bell','已记录通过；原版动画持久化仍需跟进。'],
 ['黏土圣堂武士','Assets/Resources/RainActors/PendingWhitebox/claybruiser','已记录通过；原版动画持久化仍需跟进。'],
 ['黏土药剂师','Assets/Resources/RainActors/PendingWhitebox/claygrenadier','已记录通过；原版动画持久化仍需跟进。'],
 ['魔童','Assets/Resources/RainActors/PendingWhitebox/child','已记录通过；动画曲线已烘焙；迁移后的截图见 Docs/Validation/ChildFrames。']])+
 '\n### 当前验收完成情况\n\n'+table(['状态','数量'],[['已记录通过',acceptance.isolatedPassed+'/'+acceptance.catalogTotal],['失败',acceptance.failed],['待验证或待实现',acceptance.pending],['非攻击蝙蝠跳过',acceptance.skipped],['本次资源整理新增验收','0']])+
 '\nGoal 保持暂停。91 项是历史记录；迁移检查仍记录 14 条已知渲染 keyword 错误。MiniMushroom 和 Child 的动画曲线已处理，Bell、ClayBruiser、Grenadier 的原版动画持久化仍需后续跟进。声音采样记录不等于逐项人工听辨。\n\n'+
 '## 包与插件\n\n'+table(['嵌入式包','路径'],packageDirs.map(n=>[n,'Packages/'+n]))+
 '\n注册表依赖及版本：\n\n'+table(['包','版本'],Object.entries(manifest.dependencies))+
 '\n## 完整性与重复候选\n\n'+table(['检查','结果'],[
 ['Assets 文件缺少 meta',audits.missingMeta.length],['找不到对应文件或文件夹的 meta',audits.orphanMeta.length],['重复 GUID 组',audits.duplicateGuids.length],
 ['LFS 内容相同的重复组',duplicates.length],['合并重复副本的理论体积',unit(duplicates.reduce((s,x)=>s+x.potentialBytes,0))],['Resources 同名加载键候选',resources.length],
 ['其中同类型同键候选',resources.filter(x=>x.sameType).length]])+
 '\n重复内容以当前提交的 LFS SHA256 标识核对，并非全量非 LFS 文件哈希去重。重复资源可能具有不同 GUID 或导入设置；合并前必须重定向引用并验证。Resources 同键但类型不同可能属于合法的按类型加载；完整候选记录在 JSON 中。上述结果不等于可直接删除名单。\n\n'+
 table(['Resources 加载键','资源类型','核对范围'],resources.map(x=>[x.key,x.paths.map(y=>y.type).join(' / '),x.sameType?'同类型候选，需要检查实际加载':'类型不同；检查是否始终使用带类型参数的加载']))+
 '### 体积最大的资源\n\n'+table(['路径','类型','体积'],[...assets].sort((a,b)=>b.bytes-a.bytes).slice(0,20).map(x=>[x.path,x.type,unit(x.bytes)]))+
 '\n### 重复内容样例\n\n'+(duplicates.length?table(['同内容文件路径','理论可省体积'],duplicates.slice(0,10).map(x=>[x.paths.join('；'),unit(x.potentialBytes)])):'未发现 LFS 内容重复组。\n')+
 '\n## 后续目录整理顺序\n\n'+table(['顺序','范围','执行条件'],[
 ['1','统一入口：本清单、README、实机截图表','本次已建立；不搬迁资源。'],
 ['2','大体积玩家角色与演示内容','列出当前白盒玩家 Prefab、角色选择项和动态加载依赖，再逐包裁剪；保留可用玩家。'],
 ['3','重复内容与旧导入产物','核对 GUID、导入参数、Prefab 与运行时加载键；确认引用重定向后再合并。'],
 ['4','怪物源资源、运行资产与 Editor 工具分层','保留 Resources 相对加载键，使用 Unity AssetDatabase 移动资产与 meta，同时修正工具中的绝对 Asset 路径。'],
 ['5','每批整理后的验证','重新编译、打开 Startup 和 TrainingWhitebox、检查丢失引用，再抽查受影响怪物的画面、移动、攻击与声音。']])+
 '\n本次只建立分类和索引，没有移动、重命名或删除 Assets、Packages、ProjectSettings 下的资源，也没有改写原 TPSShoot 主工程。物理目录合并与资源裁剪仍需按上述依赖核对逐批执行。\n\n'+
 '## 更新清单\n\n在仓库根目录执行 `node Tools/BuildResourceInventory.cjs`。脚本只读取 Git 跟踪的工程源文件并更新 Docs/Resources，不启动 Unity、不修改 meta、不推进怪物 Goal。新增资源需先加入 Git 跟踪，才会进入统计。\n';
fs.writeFileSync(path.join(out,'资源清单.md'),doc);
const html=`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>UnityMonster 资源清单</title><style>
*{box-sizing:border-box}body{margin:0;background:#f4f6f9;color:#1c293b;font:15px/1.6 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1450px;margin:auto;padding:30px}h1{margin:0 0 8px}a{color:#1759aa}.summary{display:flex;flex-wrap:wrap;gap:15px;margin:20px 0}.card{background:white;border:1px solid #dce3ec;border-radius:8px;padding:16px;min-width:180px}.card strong{display:block;font-size:25px}.filters{display:flex;gap:10px;flex-wrap:wrap;margin:20px 0}input,select,button{font:inherit;padding:8px;border:1px solid #b9c6d8;border-radius:5px}input{flex:1;min-width:260px}button{background:white;cursor:pointer}table{border-collapse:collapse;width:100%;background:white}th,td{padding:10px;border-bottom:1px solid #dde4ee;text-align:left;vertical-align:top}th{background:#e8eef6;white-space:nowrap}td.path{max-width:680px;overflow-wrap:anywhere;font-family:ui-monospace,monospace;font-size:13px}.muted{color:#596980;font-size:13px}.panel{background:#fff;padding:18px;border-radius:8px;margin-bottom:20px}.pager{display:flex;align-items:center;gap:12px;margin:15px 0}code{overflow-wrap:anywhere}details{margin:12px 0}#tablewrap{overflow:auto}</style>
<main><h1>UnityMonster 资源清单</h1><p>按用途、类型、目录与名称检索工程资源。目录分组与文件数量不等于已验收怪物数量。</p><p><a href="资源清单.md">分类说明与整理建议</a> · <a href="资源完整清单.json">完整 JSON</a> · <a href="../Acceptance/怪物实机截图验收汇总.html">怪物实机截图验收表</a></p>
<div class="summary"><div class="card">Assets 资源<strong>${assets.length} 个</strong>${gib(sum(assets))}</div><div class="card">历史验收<strong>${acceptance.isolatedPassed}/${acceptance.catalogTotal}</strong>本次新增 0 · Goal 暂停</div><div class="card">内容重复候选<strong>${duplicates.length} 组</strong>理论可省 ${unit(duplicates.reduce((s,x)=>s+x.potentialBytes,0))}</div><div class="card">meta 与 GUID<strong>${audits.missingMeta.length+audits.orphanMeta.length+audits.duplicateGuids.length} 项异常</strong>细项见完整 JSON</div></div>
<div class="panel"><b>重点核对</b>：CharacterSelection 占 ${gib(folders.find(x=>x.name==='Assets/Game/CharacterSelection').bytes)}，约 ${(100*folders.find(x=>x.name==='Assets/Game/CharacterSelection').bytes/sum(assets)).toFixed(1)}%。内容重复候选理论只可节省 ${unit(duplicates.reduce((s,x)=>s+x.potentialBytes,0))}。<br><b>整理边界</b>：当前建立分类清单，没有移动或删除资源。资源存在不代表已运行通过，未判定为冗余。Library、缓存、构建结果与 meta 不计入资源文件数。<div class="muted">来源提交 ${commit} · ${result.generatedAt} · 本地展开后的真实体积</div></div>
<details><summary>顶层目录体积与用途</summary><table><thead><tr><th>目录</th><th>数量</th><th>体积</th><th>用途与建议</th></tr></thead><tbody>${folders.map(g=>'<tr><td>'+escape(g.name)+'</td><td>'+g.count+'</td><td>'+unit(g.bytes)+'</td><td>'+escape(purposes[g.name]||'角色、材质或共享配套；待依赖核对')+'</td></tr>').join('')}</tbody></table></details>
<div class="filters"><input id="q" placeholder="搜索路径、名称、GUID、Resources 加载键"><select id="cat"><option value="">全部分类</option>${categories.map(g=>'<option>'+escape(g.name)+'</option>').join('')}</select><select id="kind"><option value="">全部类型</option>${[...new Set(files.map(x=>x.type))].sort().map(x=>'<option>'+escape(x)+'</option>').join('')}</select><select id="order"><option value="size">体积从大到小</option><option value="path">路径排序</option></select></div>
<p id="count"></p><div id="tablewrap"><table><thead><tr><th>资源路径与加载信息</th><th>用途分类</th><th>类型</th><th>体积</th><th>LFS</th></tr></thead><tbody id="rows"></tbody></table></div><div class="pager"><button id="prev">上一页</button><span id="page"></span><button id="next">下一页</button></div></main>
<script id="inventory" type="application/json">${JSON.stringify(files).replaceAll('<','\\u003c')}</script><script>
const data=JSON.parse(document.getElementById('inventory').textContent),el=id=>document.getElementById(id);let filtered=[],page=0;const size=100;
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
function render(){const pages=Math.max(1,Math.ceil(filtered.length/size));page=Math.min(page,pages-1);el('rows').innerHTML=filtered.slice(page*size,(page+1)*size).map(x=>'<tr><td class="path"><b>'+esc(x.path)+'</b><div class="muted">GUID: '+esc(x.guid||'不适用或无 GUID')+(x.resourcesKey?'<br>Resources: '+esc(x.resourcesKey):'')+'</div></td><td>'+esc(x.category)+'</td><td>'+esc(x.type)+'</td><td>'+(x.bytes/1048576).toFixed(2)+' MiB</td><td>'+(x.lfs?'是':'否')+'</td></tr>').join('');el('count').textContent='匹配 '+filtered.length+' 个源文件 · '+(filtered.reduce((s,x)=>s+x.bytes,0)/1073741824).toFixed(2)+' GiB';el('page').textContent=(page+1)+' / '+pages;el('prev').disabled=!page;el('next').disabled=page>=pages-1;}
function filter(){const q=el('q').value.trim().toLowerCase();filtered=data.filter(x=>(!el('cat').value||x.category===el('cat').value)&&(!el('kind').value||x.type===el('kind').value)&&(!q||(x.path+' '+x.guid+' '+x.resourcesKey).toLowerCase().includes(q)));filtered.sort(el('order').value==='size'?(a,b)=>b.bytes-a.bytes:(a,b)=>a.path.localeCompare(b.path));page=0;render();}
for(const id of ['q','cat','kind','order'])el(id).addEventListener(id==='q'?'input':'change',filter);el('prev').onclick=()=>{page--;render()};el('next').onclick=()=>{page++;render()};filter();
</script></html>`;
fs.writeFileSync(path.join(out,'资源清单.html'),html);
console.log(JSON.stringify({assets:assets.length,assetGiB:gib(sum(assets)),totalFiles:files.length,metaFiles:metas.length,topFolders:folders.slice(0,5).map(({files,...g})=>g),
 actors:actors.length,duplicateGroups:duplicates.length,duplicatePotentialMiB:unit(duplicates.reduce((s,x)=>s+x.potentialBytes,0)),missingMeta:audits.missingMeta.length,orphanMeta:audits.orphanMeta.length,
 duplicateGuidGroups:audits.duplicateGuids.length,resourcesKeyCandidates:resources.length},null,2));
