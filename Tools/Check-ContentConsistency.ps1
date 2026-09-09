#requires -Version 7
<#
.SYNOPSIS
    SlayTheStella 本地化内容一致性检查（纯脚本，不编译 C#，可在 CI 上运行）。

.DESCRIPTION
    检查四类问题：
      [error] JSON 无法解析 / 编码不是 UTF-8 无 BOM
      [error] zhs（基准语言）与代码注册类不同步：
              - 代码中有类但 zhs 缺对应键或缺 .title/.description 字段
              - zhs 中有键但代码中找不到对应类（孤儿键/残留）
      [warn]  语言间（zhs/eng/jpn）键集合不一致：某语言缺键或多键
      [warn]  语言目录文件集合不一致（如 eng/jpn 漏建某文件）

    键段规则（与 RitsuLib/BaseLib 自动注册一致）：
      类名 PascalCase -> SCREAMING_SNAKE，如 MwStrike -> MW_STRIKE、Vita -> VITA、
      MoWang -> MO_WANG，拼成 SLAY_THE_STELLA_CARD_{SEG}.title 等。
      本脚本不解析 RitsuLib 运行时代码，仅按此约定静态推导。

.PARAMETER RepoRoot
    仓库根目录，默认取脚本所在目录的上一级。

.PARAMETER Strict
    开启后把 warn 级问题一并当作失败（适合 CI 卡门禁）。

.PARAMETER Annotations
    把每条问题同时输出为 GitHub Actions workflow command annotation（::warning），
    适合"只提示、不中断构建流程"的用法。未指定时若检测到环境变量
    GITHUB_ACTIONS=true（即运行于 GitHub Actions）也会自动开启。
    开启后所有问题（含原 [error]）统一按 ::warning 呈现，且退出码恒为 0；
    若在 CI 中仍希望用退出码拦门禁，请额外显式加 -Strict。

.EXAMPLE
    pwsh ./Tools/Check-ContentConsistency.ps1
.EXAMPLE
    pwsh ./Tools/Check-ContentConsistency.ps1 -Strict   # 供 CI 卡门禁使用
.EXAMPLE
    pwsh ./Tools/Check-ContentConsistency.ps1 -Annotations   # 输出 GitHub annotation（本地预览）
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')),
    [switch]$Strict,
    [switch]$Annotations
)

$ErrorActionPreference = 'Stop'

# GitHub Actions 环境自动开启 annotation 输出；也可用 -Annotations 显式开启（本地预览）
$script:AnnotationsEnabled = $Annotations -or $env:GITHUB_ACTIONS -eq 'true'

# ---------- 常量 ----------
$LocalizationDir = Join-Path $RepoRoot 'SlayTheStella/localization'
$ScriptsDir      = Join-Path $RepoRoot 'Scripts'

# 语言目录（顺序即报告顺序）；settings 子目录单独处理
$Langs = @('zhs', 'eng', 'jpn')
$SettingsDir = 'settings'

# 每种内容对应: 文件名、键前缀、需要哪些字段
$ContentKinds = @(
    @{ Kind = 'card';       File = 'cards.json';       Prefix = 'SLAY_THE_STELLA_CARD_';       RequiredFields = @('title', 'description') }
    @{ Kind = 'relic';      File = 'relics.json';      Prefix = 'SLAY_THE_STELLA_RELIC_';      RequiredFields = @('title', 'description') }
    @{ Kind = 'potion';     File = 'potions.json';     Prefix = 'SLAY_THE_STELLA_POTION_';     RequiredFields = @('title', 'description') }
    @{ Kind = 'character';  File = 'characters.json';  Prefix = 'SLAY_THE_STELLA_CHARACTER_';  RequiredFields = @('title') }
)

# 参与"三语键集合一致"比较的文件（不含 card_keywords：仅 zhs 存在属历史现状，由文件集合检查兜底）
# settings 目录（settings/zhs|eng|jpn.json）的键集合一致性在 3.3 末尾单独比较
$CrossLangFiles = @('cards.json', 'relics.json', 'potions.json', 'characters.json', 'static_hover_tips.json')

# 代码中的直接基类 -> 内容种类
$BaseKindMap = @{
    MwCardModel          = 'card'
    MwDiscCardModel      = 'card'
    MwHarmonyCardModel   = 'card'
    MwRelicModel         = 'relic'
    MwPotionModel        = 'potion'
    ModCharacterTemplate = 'character'
}

# ---------- 输出与统计 ----------
# 各分节问题行统一以 "[error]" / "[warn]" 开头，便于肉眼区分。
# 注意：PowerShell 的 -like '[error]*' 会把 [error] 当成通配符字符类，因此判
# 断用 StartsWith；汇总计数直接按列表相加（jsonIssues/syncIssues 全为 error，
# fileWarns/crossWarns 全为 warn），避免字符串匹配歧义。

# ---------- GitHub Actions annotation ----------
# 开启后（-Annotations 或运行于 GitHub Actions），每条问题除控制台红/黄文本外，
# 额外输出一条 ::warning workflow command。::warning 只生成注解、不影响步骤退出码，
# 配合末尾"提示模式 exit 0"实现"会提示 warning 但不中断构建流程"。

function ConvertTo-GithubCommandValue([string]$Value) {
    # 按 GitHub workflow command 转义规则处理 message/file 值：% -> %25、回车 -> %0D、换行 -> %0A
    return $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
}

function Get-ProblemFile([string]$Line) {
    # 尽力把问题行映射为仓库根相对文件路径（SlayTheStella/localization/...），
    # 让 annotation 能定位到具体文件；提取失败返回空串（annotation 省略 file）。
    # 3.1 文案形如："localization/eng 缺文件: card_keywords.json"、"settings/jpn 多出文件: x.json（zhs 无此文件）"
    $m = [regex]::Match($Line, '\b(localization|settings)/(\w+) (?:缺文件|多出文件): ([^（ ]+)')
    if ($m.Success) {
        $langDir  = $m.Groups[2].Value
        $fileName = $m.Groups[3].Value
        if ($m.Groups[1].Value -eq 'settings') {
            return "SlayTheStella/localization/settings/$langDir/$fileName"
        }
        return "SlayTheStella/localization/$langDir/$fileName"
    }
    # 其余文案：取行内第一处 "目录/文件.json"（相对 localization 目录，如 zhs/cards.json、settings/eng.json）
    $p = [regex]::Match($Line, '([\w-]+/[\w-]+\.json)')
    if ($p.Success) { return "SlayTheStella/localization/$($p.Groups[1].Value)" }
    return ''
}

function Write-GithubAnnotation([string]$Line) {
    if (-not $script:AnnotationsEnabled) { return }
    $msg  = $Line -replace '^\[(error|warn)\] ', ''
    $file = Get-ProblemFile -Line $Line
    $cmd  = '::warning'
    if ($file) { $cmd += " file=$(ConvertTo-GithubCommandValue -Value $file)" }
    $cmd += '::' + (ConvertTo-GithubCommandValue -Value $msg)
    Write-Host $cmd
}

function Write-Problems([string]$Section, [string[]]$Lines) {
    if ($Lines.Count -eq 0) {
        Write-Host "  [ok] $Section"
        return
    }
    Write-Host "  [!] $Section ($($Lines.Count) 处)"
    foreach ($line in $Lines) {
        Write-GithubAnnotation -Line $line
        if ($line.StartsWith('[error]')) {
            Write-Host "      $line" -ForegroundColor Red
        } else {
            Write-Host "      $line" -ForegroundColor Yellow
        }
    }
}

# ---------- 基础工具 ----------
function Get-JsonKeys([string]$Path) {
    # 返回排好序的顶层键数组
    $obj = Get-Content -Path $Path -Raw -Encoding utf8 | ConvertFrom-Json
    return @($obj.PSObject.Properties.Name | Sort-Object)
}

function Get-JsonKeysSafely([string]$Path) {
    # 解析失败返回 $null（不抛异常）。坏 JSON 统一由 3.2 报 [error]，
    # 后续读取点（2、3.3、settings）遇到坏文件直接跳过即可，
    # 避免同一文件被多次无保护解析导致脚本在汇总前崩溃。
    try { return Get-JsonKeys -Path $Path } catch { return $null }
}

function Test-Utf8NoBom([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { return 'utf8-bom' }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) { return 'utf16-le' }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) { return 'utf16-be' }
    return 'ok'
}

function ConvertTo-KeySegment([string]$ClassName) {
    # PascalCase -> SCREAMING_SNAKE: MwTestCard -> MW_TEST_CARD
    return [regex]::Replace($ClassName, '(?<=[a-z0-9])(?=[A-Z])', '_').ToUpperInvariant()
}

# 键 "SLAY_THE_STELLA_CARD_MW_STRIKE.title" -> ("MW_STRIKE", "title")
function Split-Key([string]$Key, [string]$Prefix) {
    $rest = $Key.Substring($Prefix.Length)
    $dot = $rest.IndexOf('.')
    if ($dot -lt 0) { return , @($rest, '') }
    return , @($rest.Substring(0, $dot), $rest.Substring($dot + 1))
}

# ---------- 1. 收集代码中的内容类 ----------
$script:Classes = [System.Collections.Generic.List[object]]::new()  # @{ Name; Base; Abstract }
function Collect-Classes([string]$Dir) {
    $classRe = [regex]'(?m)^\s*(?:public|internal)\s+(?:(sealed|abstract)\s+)?class\s+(\w+)'
    foreach ($cs in Get-ChildItem -Path $Dir -Recurse -Filter *.cs) {
        $text = [System.IO.File]::ReadAllText($cs.FullName)
        foreach ($m in $classRe.Matches($text)) {
            $abstract = ($m.Groups[1].Value -eq 'abstract')
            $name     = $m.Groups[2].Value
            $brace    = $text.IndexOf('{', $m.Index)
            if ($brace -lt 0) { continue }  # 无类体，跳过
            $header   = $text.Substring($m.Index, $brace - $m.Index)
            $bm = [regex]::Match($header, ':\s*([A-Za-z_]\w*)')
            $base = if ($bm.Success) { $bm.Groups[1].Value } else { '' }
            if ($base -and $BaseKindMap.ContainsKey($base)) {
                $script:Classes.Add(@{ Name = $name; Base = $base; Abstract = $abstract; File = $cs.Name })
            }
        }
    }
}
Collect-Classes -Dir $ScriptsDir

# ---------- 2. 解析 zhs 内容键 ----------
# $zhsSegments[kind] = @{ segment = @{ field1; field2; ... } }（保序用有序字典即可，此处无所谓）
$zhsSegments = @{}
foreach ($kindInfo in $ContentKinds) {
    $zhsSegments[$kindInfo.Kind] = [System.Collections.Generic.Dictionary[string, object]]::new()
}
foreach ($kindInfo in $ContentKinds) {
    $file = Join-Path $LocalizationDir "zhs/$($kindInfo.File)"
    if (-not (Test-Path $file)) {
        # zhs 缺基准文件时，3.4 的代码类循环会逐类报"键不存在"，这里无需重复报
        continue
    }
    $keys = Get-JsonKeysSafely -Path $file
    if ($null -eq $keys) { continue }  # zhs 基准文件坏 JSON：由 3.2 报 [error]，此处跳过
    foreach ($key in $keys) {
        if (-not $key.StartsWith($kindInfo.Prefix)) { continue }
        $seg = Split-Key -Key $key -Prefix $kindInfo.Prefix
        if (-not $zhsSegments[$kindInfo.Kind].ContainsKey($seg[0])) {
            $zhsSegments[$kindInfo.Kind][$seg[0]] = [System.Collections.Generic.HashSet[string]]::new()
        }
        [void]$zhsSegments[$kindInfo.Kind][$seg[0]].Add($seg[1])
    }
}

# ---------- 3. 检查区 ----------
Write-Host ''
Write-Host '======== SlayTheStella 本地化内容一致性检查 ========' -ForegroundColor Cyan
Write-Host "仓库根: $RepoRoot"
Write-Host ''

# --- 3.1 语言目录文件集合一致 ---
Write-Host '[1/4] 语言目录文件集合一致（含 settings）' -ForegroundColor Cyan
$fileWarns = [System.Collections.Generic.List[string]]::new()
foreach ($group in @(@{ Label = 'localization'; Root = $LocalizationDir }, @{ Label = 'settings'; Root = Join-Path $LocalizationDir $SettingsDir })) {
    $fileSets = @{}
    foreach ($lang in $Langs) {
        $dir = Join-Path $group.Root $lang
        $fileSets[$lang] = if (Test-Path $dir) { @(Get-ChildItem -Path $dir -Filter *.json | ForEach-Object Name | Sort-Object) } else { @() }
    }
    $base = $fileSets['zhs']
    foreach ($lang in @('eng', 'jpn')) {
        foreach ($f in $base) {
            if ($f -notin $fileSets[$lang]) {
                $fileWarns.Add("[warn] $($group.Label)/$lang 缺文件: $f")
            }
        }
        foreach ($f in $fileSets[$lang]) {
            if ($f -notin $base) {
                $fileWarns.Add("[warn] $($group.Label)/$lang 多出文件: $f（zhs 无此文件）")
            }
        }
    }
}
Write-Problems -Section 'zhs/eng/jpn 文件集合' -Lines $fileWarns

# --- 3.2 JSON 合法性 + 无 BOM ---
Write-Host '[2/4] JSON 可解析且为 UTF-8 无 BOM' -ForegroundColor Cyan
$jsonIssues = [System.Collections.Generic.List[string]]::new()
foreach ($group in @($LocalizationDir, (Join-Path $LocalizationDir $SettingsDir))) {
    foreach ($json in Get-ChildItem -Path $group -Recurse -Filter *.json) {
        $rel = $json.FullName.Substring($LocalizationDir.Length + 1)
        $enc = Test-Utf8NoBom -Path $json.FullName
        if ($enc -ne 'ok') {
            $jsonIssues.Add("[error] $rel 编码为 $enc，约定为 UTF-8 无 BOM")
        }
        try {
            [void](Get-JsonKeys -Path $json.FullName)
        } catch {
            $jsonIssues.Add("[error] $rel JSON 解析失败: $($_.Exception.Message)")
        }
    }
}
Write-Problems -Section 'JSON 文件' -Lines $jsonIssues

# --- 3.3 三语键集合一致（内容文件 + settings） ---
Write-Host '[3/4] zhs/eng/jpn 键集合一致（内容文件 + settings）' -ForegroundColor Cyan
$crossWarns = [System.Collections.Generic.List[string]]::new()
foreach ($file in $CrossLangFiles) {
    $zhsFile = Join-Path $LocalizationDir "zhs/$file"
    if (-not (Test-Path $zhsFile)) { continue }
    $baseKeys = Get-JsonKeys -Path $zhsFile
    foreach ($lang in @('eng', 'jpn')) {
        $langFile = Join-Path $LocalizationDir "$lang/$file"
        if (-not (Test-Path $langFile)) { continue }
        $langKeys = Get-JsonKeysSafely -Path $langFile
        if ($null -eq $langKeys) { continue }  # 坏 JSON：3.2 已报 [error]，跳过该语言文件
        $missing = @($baseKeys | Where-Object { $_ -notin $langKeys })
        $extra   = @($langKeys | Where-Object { $_ -notin $baseKeys })
        foreach ($k in $missing) {
            $crossWarns.Add("[warn] $lang/$file 缺键: $k")
        }
        foreach ($k in $extra) {
            $crossWarns.Add("[warn] $lang/$file 多出键（zhs 无）: $k")
        }
    }
}
# settings 目录：settings/zhs.json 与 eng/jpn.json 键集合一致（settings.title 等）
foreach ($lang in @('eng', 'jpn')) {
    $sBase = Join-Path $LocalizationDir "$SettingsDir/zhs.json"
    $sLang = Join-Path $LocalizationDir "$SettingsDir/$lang.json"
    if (-not (Test-Path $sBase) -or -not (Test-Path $sLang)) { continue }  # 缺文件已由 3.1 报告
    $sBaseKeys = Get-JsonKeysSafely -Path $sBase
    if ($null -eq $sBaseKeys) { continue }  # settings/zhs.json 坏 JSON：3.2 已报 [error]
    $sLangKeys = Get-JsonKeysSafely -Path $sLang
    if ($null -eq $sLangKeys) { continue }  # settings/{lang}.json 坏 JSON：3.2 已报 [error]
    foreach ($k in @($sBaseKeys | Where-Object { $_ -notin $sLangKeys })) {
        $crossWarns.Add("[warn] $SettingsDir/$lang.json 缺键: $k")
    }
    foreach ($k in @($sLangKeys | Where-Object { $_ -notin $sBaseKeys })) {
        $crossWarns.Add("[warn] $SettingsDir/$lang.json 多出键（zhs 无）: $k")
    }
}
# 报告行可能很多，聚合成"缺 N 键"摘要 + 前 10 条明细
Write-Problems -Section '三语键集合' -Lines $crossWarns

# --- 3.4 代码注册类 <-> zhs 本地化 双向覆盖 ---
Write-Host '[4/4] 代码注册类 ↔ zhs 本地化键 双向覆盖' -ForegroundColor Cyan
$syncIssues = [System.Collections.Generic.List[string]]::new()
$kindByClass = @{}   # className -> kind（记录首个归属，便于孤儿检查）
foreach ($c in $script:Classes) {
    if ($c.Abstract) { continue }  # 抽象基类（Models/*）不产生内容
    $kind = $BaseKindMap[$c.Base]
    if (-not $kind) { continue }
    $kindByClass[$c.Name] = $kind

    $kindInfo = $ContentKinds | Where-Object Kind -eq $kind
    $prefix   = $kindInfo.Prefix
    $seg      = ConvertTo-KeySegment -ClassName $c.Name
    $zhFile   = Join-Path $LocalizationDir "zhs/$($kindInfo.File)"

    if (-not $zhsSegments[$kind].ContainsKey($seg)) {
        $syncIssues.Add("[error] 代码类 $($c.Name)（$($c.File)）→ 键 $prefix$seg 在 zhs/$($kindInfo.File) 中不存在")
        continue
    }
    $haveFields = $zhsSegments[$kind][$seg]
    foreach ($field in $kindInfo.RequiredFields) {
        if ($field -notin $haveFields) {
            $syncIssues.Add("[error] $prefix$seg 缺 .$field 字段（zhs/$($kindInfo.File)，代码类 $($c.Name)）")
        }
    }
}
# 反向：zhs 中每段应能在代码中找到类（孤儿键）
foreach ($kindInfo in $ContentKinds) {
    if (-not $zhsSegments[$kindInfo.Kind]) { continue }
    foreach ($seg in $zhsSegments[$kindInfo.Kind].Keys) {
        $found = $false
        foreach ($c in $script:Classes) {
            if ($c.Abstract) { continue }
            if ($BaseKindMap[$c.Base] -eq $kindInfo.Kind -and
                (ConvertTo-KeySegment -ClassName $c.Name) -eq $seg) {
                $found = $true; break
            }
        }
        if (-not $found) {
            $syncIssues.Add("[error] zhs/$($kindInfo.File) 键段 $seg 无对应代码类（孤儿键/残留？）")
        }
    }
}
Write-Problems -Section '代码 ↔ zhs 覆盖' -Lines $syncIssues

# ---------- 汇总与退出码 ----------
$totalErr  = $jsonIssues.Count + $syncIssues.Count
$totalWarn = $fileWarns.Count + $crossWarns.Count

Write-Host ''
Write-Host "======== 结果汇总: error $totalErr / warn $totalWarn ========" -ForegroundColor Cyan
if ($script:AnnotationsEnabled) {
    Write-Host '（GitHub Actions 提示模式：问题以 ::warning annotation 输出，不阻断构建；如需门禁请加 -Strict）' -ForegroundColor DarkGray
}

if ($Strict) {
    $failed = ($totalErr + $totalWarn) -gt 0
} else {
    $failed = $totalErr -gt 0
}
if ($failed) {
    if ($script:AnnotationsEnabled -and -not $Strict) {
        # GitHub Actions 提示模式：annotation 已在上方输出，仅提示、不中断构建流程
        exit 0
    }
    Write-Host '检查未通过。' -ForegroundColor Red
    exit 1
}
Write-Host '检查通过。' -ForegroundColor Green
exit 0
