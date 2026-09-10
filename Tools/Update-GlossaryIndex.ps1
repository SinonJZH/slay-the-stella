<#
.SYNOPSIS
    重新生成 _manual/stella-sora-glossary.md 开头的「目录索引（行号导航）」标记块。

.DESCRIPTION
    纯脚本、不硬编码任何章节清单：直接扫描 markdown 正文各级标题（## ~ ######），
    重算每个小节的行区间，然后重写文件头 <!-- glossary-index:begin/end --> 标记块
    内的内容。章节增删、改名、挪位均可自适应。

    规则：
    - 索引块以注释标记为界，块内内容全部由本脚本生成，勿手改；
    - 章节条目 = 正文 ## ~ ###### 标题各一行（H1 大标题与索引块自身不进表）；
      区间终点 = 下一个标题行 - 2（吃掉标题前的空行），末节到文件末尾；
      章级（##）区间自然覆盖其子节，与子节区间重叠是有意为之，便于整章读取；
    - 「内容提要」列按章节标题精确匹配保留：从旧表解析后随新表写回。标题改名/
      删除的旧提要从索引移除并 [warn] 提示；新章节提要留空并提示，手工补进
      表后再次运行即可长期保留；
    - 写回保持原文件换行风格（LF/CRLF）与 UTF-8 无 BOM。

.PARAMETER MarkdownPath
    目标 markdown 文件，默认取脚本所在目录上一级的 _manual/stella-sora-glossary.md。

.PARAMETER Check
    只校验不写盘：索引过期时退出码 1（将来接 CI 门禁用），否则 0。

.EXAMPLE
    pwsh ./Tools/Update-GlossaryIndex.ps1            # 正文改动后刷新索引行号
.EXAMPLE
    pwsh ./Tools/Update-GlossaryIndex.ps1 -Check     # 校验索引是否最新
#>
[CmdletBinding()]
param(
    [string]$MarkdownPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')) '_manual/stella-sora-glossary.md'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'

# 控制台输出按 UTF-8，避免中文提示在重定向/CI 日志里乱码
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not (Test-Path -LiteralPath $MarkdownPath)) {
    Write-Host "[error] 文件不存在：$MarkdownPath"
    exit 1
}
$path = (Resolve-Path -LiteralPath $MarkdownPath).Path
# 读写一律显式 UTF-8：若依赖代码页默认值，Windows PowerShell 5.1 会把无 BOM 文件按
# 系统 ANSI（如 GBK）解码，中文会被双重编码写坏。脚本文件自身带 BOM 同样是为了
# 5.1 能正确解析下面的中文字面量（pwsh 7 两者都兼容）。
$utf8  = [System.Text.UTF8Encoding]::new($false)
$raw   = [System.IO.File]::ReadAllText($path, $utf8)
$nl    = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
$lines = [System.IO.File]::ReadAllLines($path, $utf8)

$beginMarker = '<!-- glossary-index:begin 由 Tools/Update-GlossaryIndex.ps1 生成，标记块内勿手改（「内容提要」列刷新时按章节标题保留） -->'
$endMarker   = '<!-- glossary-index:end -->'

# ---------- 1. 定位旧索引块 ----------
# 优先找注释标记块；兼容无标记的旧式「## 目录索引」标题（至其后第一个裸 ---，含）；
# 都没有则视为首次接入：插到文件中第一个裸 --- 之后。
$begin = -1
$end   = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($begin -lt 0) {
        if ($lines[$i] -match 'glossary-index:begin') { $begin = $i; continue }
        if ($lines[$i] -match '^##\s*目录索引') {
            $begin = $i
            for ($j = $i + 1; $j -lt $lines.Count; $j++) {
                if ($lines[$j] -eq '---') { $end = $j; break }
            }
            if ($end -lt 0) {
                Write-Host '[error] 旧式索引块未闭合（其后找不到裸 --- 结束线）'
                exit 1
            }
            break
        }
    } elseif ($lines[$i] -match 'glossary-index:end') {
        $end = $i; break
    }
}
$insertMode = $false
if ($begin -lt 0) {
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -eq '---') { $begin = $i + 1; $end = $i; $insertMode = $true; break }
    }
}
if ($begin -lt 0) {
    Write-Host '[error] 文件中找不到裸 --- 分隔线可作插入锚点'
    exit 1
}

$pre = @(); if ($begin -gt 0) { $pre += $lines[0..($begin - 1)] }
$oldBlock = @(); if ($end -ge $begin) { $oldBlock += $lines[$begin..$end] }
$post = @(); if ($end + 1 -lt $lines.Count) { $post += $lines[($end + 1)..($lines.Count - 1)] }
if ($insertMode) { $pre += '' }   # --- 之后补一个空行再放索引块

# ---------- 2. 解析旧表，保留「内容提要」（键 = 去缩进后的章节标题） ----------
$oldSummary = @{}
foreach ($ln in $oldBlock) {
    if ($ln -match '^\|\s*\d+\s*[–-]\s*\d+\s*\|') {
        $cells = $ln -split '\|'
        if ($cells.Count -ge 5) {
            $sec = $cells[2].Trim().TrimStart([char]0x3000).Trim()
            $sumParts = for ($c = 3; $c -le $cells.Count - 2; $c++) { $cells[$c] }
            if ($sec) { $oldSummary[$sec] = ($sumParts -join '|').Trim() }
        }
    }
}

# ---------- 3. 扫描正文标题（索引块已切走不参与；H1 大标题不进表） ----------
$heads = @()
for ($i = 0; $i -lt $post.Count; $i++) {
    if ($post[$i] -match '^(#{2,6})\s+(.+?)\s*$') {
        $heads += [pscustomobject]@{ Level = $Matches[1].Length; Text = $Matches[2]; PostIdx = $i }
    }
}

# ---------- 4. 生成新索引块 ----------
# 块长与行号数值无关（每条目固定占一行），可先定块长再填行号：
# 固定 11 行（begin/标题/空行/引言x2/空行/表头/表分隔/空行/---/end）
# + 「文件头」行 + 每标题一行
$blockCount = 12 + $heads.Count
$total      = $pre.Count + $blockCount + $post.Count
$baseLine   = $pre.Count + $blockCount + 1     # post[0] 所在行号（1 基）

$rows = [System.Collections.Generic.List[string]]::new()
$headSum = if ($oldSummary.ContainsKey('文件头')) { $oldSummary['文件头'] } else { '标题、用途、数据来源、标记约定' }
$rows.Add(('| 1–{0} | 文件头 | {1} |' -f [Math]::Max(1, $pre.Count - 1), $headSum))

for ($k = 0; $k -lt $heads.Count; $k++) {
    $h     = $heads[$k]
    $start = $baseLine + $h.PostIdx
    $stop  = if ($k + 1 -lt $heads.Count) { $baseLine + $heads[$k + 1].PostIdx - 2 } else { $total }
    if ($stop -lt $start) { $stop = $start }
    $indent = '　' * [Math]::Max(0, $h.Level - 2)
    $sum = if ($oldSummary.ContainsKey($h.Text)) { $oldSummary[$h.Text] } else { '' }
    $rows.Add(('| {0}–{1} | {2}{3} | {4} |' -f $start, $stop, $indent, $h.Text, $sum))
}

$q1 = '> **使用方式**：查术语先读本表定位目标小节的行区间，读取文件只读对应区间，无需读取整个文件（当前全文共 ' + $total + ' 行）；也可直接 `grep -F "词条"` 命中整行表格，一次拿齐中/英/日三语对照。'
$q2 = '> **行号维护**：本标记块由 `Tools/Update-GlossaryIndex.ps1` 生成，勿手改；正文增删行后运行 `pwsh ./Tools/Update-GlossaryIndex.ps1` 刷新行号，「内容提要」列按章节标题自动保留。'

$newBlock  = @($beginMarker, '## 目录索引（行号导航）', '', $q1, $q2, '', '| 行区间 | 章节 | 内容提要 |', '| --- | --- | --- |')
$newBlock += $rows
$newBlock += @('', '---', $endMarker)

$newLines = @($pre) + @($newBlock) + @($post)

# ---------- 5. 提要流失/缺失提示 ----------
$usedKeys = @('文件头') + @($heads | ForEach-Object { $_.Text })
foreach ($key in @($oldSummary.Keys)) {
    if ($usedKeys -notcontains $key) {
        Write-Host "[warn] 章节已改名/删除，旧提要不再保留：「$key」=> $($oldSummary[$key])"
    }
}
$missing = @($heads | Where-Object { -not $oldSummary.ContainsKey($_.Text) } | ForEach-Object { $_.Text })
if ($missing.Count -gt 0) {
    Write-Host ('[warn] {0} 个章节暂无内容提要（手工补进索引表后重跑本脚本即可保留）：{1}' -f $missing.Count, ($missing -join '、'))
}

# ---------- 6. 幂等比较 / 写回 ----------
if (($lines -join $nl) -eq ($newLines -join $nl)) {
    Write-Host ('[ok] 索引已是最新：文件头 + {0} 个章节条目，全文 {1} 行' -f $heads.Count, $total)
    exit 0
}
if ($Check) {
    Write-Host '[error] 术语表索引行号已过期：运行 pwsh ./Tools/Update-GlossaryIndex.ps1 刷新后再提交'
    exit 1
}
[System.IO.File]::WriteAllText($path, ($newLines -join $nl) + $nl, $utf8)
Write-Host ('[ok] 索引已重写：文件头 + {0} 个章节条目，全文 {1} 行，索引块位于第 {2}–{3} 行' -f $heads.Count, $total, ($pre.Count + 1), ($pre.Count + $blockCount))
