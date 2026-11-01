#!/usr/bin/env python3
"""ZenithReborn 资产完整性静态检查（纯标准库，不启游戏、不编译）。

一、用途
    0.3.127 首装包在客户端加载阶段崩溃：MissingResourceException: FlyingBladeShot。
    原因：新弹幕类 FlyingBladeShot 既没有同名 png，也没有写 Texture override，
    tModLoader 在 AutoStaticDefaults() 里 ModContent.Request<Texture2D>(Texture) 找不到资产即崩。
    本脚本在打包前静态扫描源码，找出"会被注册、但拿不到贴图"的类型。

二、为什么隔离服务端夹具拦不住
    ModContent.HasAsset 在 -server 下恒返回 false，服务端根本不校验贴图，
    所以夹具全绿也代表不了客户端能加载。只有静态检查能覆盖这条路径。

三、已确认的 tModLoader 规则（直接使用，不再查证）
    默认贴图路径来自 ModTexturedType.Texture：
        public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');
    即资产键 = <命名空间点号换成斜杠>/<类名>，tModLoader 再在文件系统里去掉
    <模组名>/ 前缀定位文件。本仓库命名空间、模组名、模组文件夹名三者都是 ZenithReborn，
    所以期望文件恒为 <模组根>/<类名>.png（例：ZenithReborn/FlyingBladeShot.png）。
    Assets/ 下的 png 不参与默认查找。

四、判定规则
    1. 本类或任一祖先类的类体内出现 `Texture =>`（含 `override string Texture { get; } =>`）
       视为有显式 override，PASS。
    2. 否则要求文件系统存在 <模组根>/<命名空间路径>/<类名>.png，存在则 PASS。
    3. 都不满足 → 报缺口，输出 `文件:行号  类名  → 期望路径`，退出码 1。
    4. override 表达式含 `?`（三元）时，其中每个字符串字面量都当资产引用：
       `Terraria/` 开头的原版路径放过但汇总打印；`ZenithReborn/xxx` 形状的要求 .png 存在。
    5. 既无 override、又在 Assets/ 下有同名 png 的，打警告（不算失败）。

五、实现要点
    先去注释（保留字符串字面量），再把字符串内容"遮蔽"成空格用于结构解析，
    避免字符串里的 { } 与 // 干扰花括号配平；类体用花括号配平取范围，
    嵌套类型体从外层类体中剔除；partial 同名分片合并后再判定。
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

# 需要贴图的 tModLoader 基类（ModPlayer / ModSystem 等不需要，不检查）
TEXTURED_ROOTS = ("ModItem", "ModProjectile", "ModBuff", "ModNPC", "ModDust")
# 扫描时跳过的目录
SKIP_DIRS = {"bin", "obj", ".git", ".vs", ".idea", "node_modules"}


# ---------------------------------------------------------------------------
# 一、词法预处理：去注释（保留字符串）、遮蔽字符串（保留偏移）
# ---------------------------------------------------------------------------

def _char_literal_end(src: str, i: int) -> int:
    """返回从 i 处的字符字面量之后的位置。"""
    n = len(src)
    i += 1
    while i < n:
        if src[i] == "\\":
            i += 2
            continue
        if src[i] == "'":
            return i + 1
        i += 1
    return n


def _interpolation_end(src: str, i: int) -> int:
    """从 i 处的 '{' 开始，跳过插值块，返回配对 '}' 之后的位置。"""
    n = len(src)
    depth = 0
    while i < n:
        ch = src[i]
        if ch == '"' or (ch in "$@" and _looks_like_string_start(src, i)):
            i = _string_end(src, i)
            continue
        if ch == "'":
            i = _char_literal_end(src, i)
            continue
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return n


def _looks_like_string_start(src: str, i: int) -> bool:
    """判断 i 处的 $ / @ 是否是字符串前缀。"""
    j = i
    n = len(src)
    while j < n and src[j] in "$@":
        j += 1
    return j < n and src[j] == '"'


def _string_end(src: str, i: int) -> int:
    """返回从 i 处字符串（可带 $ / @ 前缀）之后的位置。"""
    n = len(src)
    verbatim = interpolated = False
    while i < n and src[i] in "$@":
        if src[i] == "$":
            interpolated = True
        else:
            verbatim = True
        i += 1
    if i >= n or src[i] != '"':
        return i
    i += 1
    while i < n:
        ch = src[i]
        if verbatim:
            if ch == '"':
                if i + 1 < n and src[i + 1] == '"':
                    i += 2
                    continue
                return i + 1
            i += 1
            continue
        if ch == "\\":
            i += 2
            continue
        if ch == '"':
            return i + 1
        if interpolated and ch == "{":
            if i + 1 < n and src[i + 1] == "{":
                i += 2
                continue
            i = _interpolation_end(src, i)
            continue
        if interpolated and ch == "}" and i + 1 < n and src[i + 1] == "}":
            i += 2
            continue
        i += 1
    return n


def strip_comments(src: str) -> str:
    """去掉 // 与 /* */ 注释，注释字符换成空格（换行保留），字符串字面量原样保留。"""
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        ch = src[i]
        if ch == "/" and i + 1 < n and src[i + 1] == "/":
            j = i
            while j < n and src[j] != "\n":
                out[j] = " "
                j += 1
            i = j
            continue
        if ch == "/" and i + 1 < n and src[i + 1] == "*":
            j = i
            out[j] = out[j + 1] = " "
            j += 2
            while j < n:
                if src[j] == "*" and j + 1 < n and src[j + 1] == "/":
                    out[j] = out[j + 1] = " "
                    j += 2
                    break
                if src[j] != "\n":
                    out[j] = " "
                j += 1
            i = j
            continue
        if ch == '"' or (ch in "$@" and _looks_like_string_start(src, i)):
            end = _string_end(src, i)
            if end > i + 1:
                i = end
                continue
        if ch == "'":
            end = _char_literal_end(src, i)
            if end > i + 1:
                i = end
                continue
        i += 1
    return "".join(out)


def blank_literals(src: str) -> str:
    """把字符串/字符字面量内容替换成空格（保留引号与偏移），供结构解析使用。"""
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        ch = src[i]
        start = -1
        if ch == '"' or (ch in "$@" and _looks_like_string_start(src, i)):
            start = i
            end = _string_end(src, i)
        elif ch == "'":
            start = i
            end = _char_literal_end(src, i)
        if start >= 0:
            for j in range(start + 1, max(start + 1, end - 1)):
                if out[j] != "\n":
                    out[j] = " "
            i = end
            continue
        i += 1
    return "".join(out)


# ---------------------------------------------------------------------------
# 二、结构解析：类声明、类体范围、嵌套类型、partial 合并
# ---------------------------------------------------------------------------

MODIFIERS = (
    "public|private|protected|internal|static|sealed|abstract|partial|unsafe|new|readonly|file|ref"
)
CLASS_HEAD_RE = re.compile(r"\b(?:(?:%s)\s+)*class\s+([A-Za-z_]\w*)" % MODIFIERS)
NAMESPACE_RE = re.compile(r"\bnamespace\s+([A-Za-z_][\w.]*)\s*[;{]")


def match_brace(text: str, open_idx: int, limit: int) -> int:
    """返回与 open_idx 处 '{' 配对的 '}' 下标，失败返回 -1。"""
    depth = 0
    for i in range(open_idx, min(limit, len(text))):
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return i
    return -1


def find_body_open(text: str, pos: int, limit: int) -> int:
    """跳过类头（可能含基类列表/泛型），返回类体 '{' 的下标。"""
    paren = bracket = angle = 0
    for i in range(pos, min(limit, len(text))):
        ch = text[i]
        if ch in "([":
            paren += 1 if ch == "(" else 0
            bracket += 1 if ch == "[" else 0
        elif ch in ")]":
            paren -= 1 if ch == ")" else 0
            bracket -= 1 if ch == "]" else 0
        elif ch == "<":
            angle += 1
        elif ch == ">":
            angle = max(0, angle - 1)
        elif ch == "{" and paren == 0 and bracket == 0 and angle == 0:
            return i
        elif ch == ";" and angle == 0:
            # 前向声明或其它语句，放弃
            return -1
    return -1


def split_top_level(text: str, sep: str = ",") -> list[str]:
    """按顶层分隔符切分（忽略 <> () [] 内的分隔符）。"""
    parts: list[str] = []
    depth = 0
    buf: list[str] = []
    for ch in text:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth = max(0, depth - 1)
        if ch == sep and depth == 0:
            parts.append("".join(buf))
            buf = []
            continue
        buf.append(ch)
    parts.append("".join(buf))
    return parts


def parse_bases(header: str) -> list[str]:
    """从类头里解析基类名（去掉泛型参数与命名空间前缀，保留顺序）。"""
    head = header.split("where")[0]
    if ":" not in head:
        return []
    raw = head.split(":", 1)[1]
    bases = []
    for item in split_top_level(raw):
        name = item.strip().split("<")[0].strip()
        if not name:
            continue
        name = name.split(":")[0].strip()
        bases.append(name.split(".")[-1])
    return bases


class TypePart:
    """一个类声明分片（partial 可能有多片）。"""

    __slots__ = ("path", "name", "chain", "bases", "abstract", "line", "spans", "body_start", "body_end")

    def __init__(self, path, name, chain, bases, abstract, line, spans, body_start, body_end):
        self.path = path
        self.name = name
        self.chain = chain
        self.bases = bases
        self.abstract = abstract
        self.line = line
        self.spans = spans
        self.body_start = body_start
        self.body_end = body_end

    @property
    def key(self) -> str:
        return ".".join(tuple(self.chain) + (self.name,))


def parse_types(masked: str, start: int, end: int, path: str, lines, chain=()) -> list[TypePart]:
    """递归解析 [start, end) 内的类声明，返回所有分片（含嵌套类型）。"""
    found: list[TypePart] = []
    pos = start
    while True:
        m = CLASS_HEAD_RE.search(masked, pos, end)
        if not m:
            break
        body_open = find_body_open(masked, m.end(), end)
        if body_open < 0:
            break
        body_close = match_brace(masked, body_open, end)
        if body_close < 0:
            break

        name = m.group(1)
        header = masked[m.end():body_open]
        bases = parse_bases(header)
        abstract = "abstract" in m.group(0)
        line = line_of(lines, m.start())

        inner = parse_types(masked, body_open + 1, body_close, path, lines, chain + (name,))

        # 外层类体的"自身成员"范围 = 类体去掉所有嵌套类型体
        cuts = sorted((p.body_start, p.body_end + 1) for p in inner)
        spans: list[tuple[int, int]] = []
        cur = body_open + 1
        for a, b in cuts:
            if a > cur:
                spans.append((cur, a))
            cur = max(cur, b)
        if cur < body_close:
            spans.append((cur, body_close))

        found.append(
            TypePart(path, name, chain, bases, abstract, line, spans, body_open, body_close)
        )
        found.extend(inner)
        pos = body_close + 1
    return found


def line_of(lines: list[int], offset: int) -> int:
    """偏移量转行号（1 起）。"""
    lo, hi = 0, len(lines) - 1
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if lines[mid] <= offset:
            lo = mid
        else:
            hi = mid - 1
    return lo + 1


def line_starts(text: str) -> list[int]:
    starts = [0]
    for i, ch in enumerate(text):
        if ch == "\n":
            starts.append(i + 1)
    return starts


# ---------------------------------------------------------------------------
# 三、Texture override 识别
# ---------------------------------------------------------------------------

TEXTURE_WORD_RE = re.compile(r"\bTexture\b")


def _expr_until_semicolon(masked: str, i: int) -> int:
    depth = 0
    while i < len(masked):
        ch = masked[i]
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
            if depth < 0:
                return i
        elif ch == ";" and depth == 0:
            return i
        i += 1
    return i


def _skip_ws(text: str, i: int) -> int:
    while i < len(text) and text[i].isspace():
        i += 1
    return i


def find_texture_override(masked: str, clean: str):
    """在类体（仅自身成员）里找 Texture 属性，返回 (clean_expr, masked_expr) 或 None。"""
    for m in TEXTURE_WORD_RE.finditer(masked):
        i = _skip_ws(masked, m.end())
        if masked.startswith("=>", i):
            start = i + 2
            stop = _expr_until_semicolon(masked, start)
            return clean[start:stop], masked[start:stop]
        if i < len(masked) and masked[i] == "{":
            close = match_brace(masked, i, len(masked))
            if close < 0:
                continue
            after = _skip_ws(masked, close + 1)
            if masked.startswith("=>", after):
                start = after + 2
                stop = _expr_until_semicolon(masked, start)
                return clean[start:stop], masked[start:stop]
            # 另外两种写法：{ get => 表达式; } 与 { get { return 表达式; } }
            body_masked = masked[i:close + 1]
            body_clean = clean[i:close + 1]
            inner = re.search(r"\bget\s*=>", body_masked)
            if inner:
                start = inner.end()
                stop = _expr_until_semicolon(body_masked, start)
                return body_clean[start:stop], body_masked[start:stop]
            inner = re.search(r"\breturn\b", body_masked)
            if inner:
                start = inner.end()
                stop = _expr_until_semicolon(body_masked, start)
                return body_clean[start:stop], body_masked[start:stop]
            return "", ""
    return None


STRING_LITERAL_RE = re.compile(r'"((?:\\.|[^"\\])*)"')


def string_literals(clean_expr: str) -> list[str]:
    """取表达式里的字符串字面量（含 @"..." 形式，按出现顺序去重）。"""
    out: list[str] = []
    for m in STRING_LITERAL_RE.finditer(clean_expr):
        value = m.group(1)
        if value not in out:
            out.append(value)
    return out


# ---------------------------------------------------------------------------
# 四、工程模型
# ---------------------------------------------------------------------------

class ModClass:
    """合并后的类型（partial 多片合一）。"""

    def __init__(self, part: TypePart):
        self.key = part.key
        self.name = part.name
        self.chain = part.chain
        self.bases = part.bases
        self.parts: list[TypePart] = [part]
        self.abstract = part.abstract
        # 代表分片：优先"文件名与类名相同"的那一片，报缺口时指向最该看的位置
        self.rep = part

    def merge(self, part: TypePart) -> None:
        self.parts.append(part)
        self.abstract = self.abstract or part.abstract
        if not self.bases and part.bases:
            self.bases = part.bases
        if self.rep.path.stem != self.name and part.path.stem == self.name:
            self.rep = part

    @property
    def decl_path(self) -> Path:
        return self.rep.path

    @property
    def decl_line(self) -> int:
        return self.rep.line

    def member_text(self, kind: str) -> str:
        """把各分片的自身成员范围拼起来；kind = clean / masked。"""
        chunks = []
        for part in self.parts:
            clean, masked = SOURCE_CACHE[part.path]
            text = clean if kind == "clean" else masked
            for a, b in part.spans:
                chunks.append(text[a:b])
            chunks.append("\n")
        return "".join(chunks)


SOURCE_CACHE: dict[Path, tuple[str, str]] = {}
NAMESPACE_CACHE: dict[Path, str] = {}


def collect_sources(root: Path) -> list[Path]:
    files: list[Path] = []
    for path in sorted(root.rglob("*.cs")):
        if any(part in SKIP_DIRS for part in path.relative_to(root).parts[:-1]):
            continue
        files.append(path)
    return files


def read_source(path: Path) -> tuple[str, str]:
    """返回 (去注释文本, 字符串已遮蔽文本)，偏移一致。"""
    if path in SOURCE_CACHE:
        return SOURCE_CACHE[path]
    raw = path.read_text(encoding="utf-8-sig", errors="replace")
    clean = strip_comments(raw)
    masked = blank_literals(clean)
    SOURCE_CACHE[path] = (clean, masked)
    return clean, masked


def namespace_of(path: Path, masked: str) -> str:
    if path in NAMESPACE_CACHE:
        return NAMESPACE_CACHE[path]
    m = NAMESPACE_RE.search(masked)
    ns = m.group(1) if m else ""
    NAMESPACE_CACHE[path] = ns
    return ns


# ---------------------------------------------------------------------------
# 五、判定
# ---------------------------------------------------------------------------

def inherits_textured(cls: ModClass, index: dict[str, ModClass]) -> bool:
    """是否在 ModItem/ModProjectile/ModBuff/ModNPC/ModDust 之下（含间接继承）。"""
    stack = [cls]
    seen: set[str] = set()
    while stack:
        cur = stack.pop()
        if cur.key in seen:
            continue
        seen.add(cur.key)
        for base in cur.bases:
            if base in TEXTURED_ROOTS:
                return True
            nxt = index.get(base)
            if nxt is not None:
                stack.append(nxt)
    return False


def override_source(cls: ModClass, index: dict[str, ModClass]):
    """返回提供 Texture override 的类（自身或祖先），没有返回 None。"""
    stack = [cls]
    seen: set[str] = set()
    while stack:
        cur = stack.pop(0)
        if cur.key in seen:
            continue
        seen.add(cur.key)
        expr = find_texture_override(cur.member_text("masked"), cur.member_text("clean"))
        if expr is not None:
            return cur, expr
        for base in cur.bases:
            nxt = index.get(base)
            if nxt is not None:
                stack.append(nxt)
    return None


def expected_asset_key(ns: str, name: str) -> str:
    """默认 Texture 的资产键：命名空间点号换斜杠 + 类名。"""
    parts = [p for p in ns.split(".") if p]
    return "/".join(parts + [name])


def asset_key_to_file(root: Path, key: str, mod_name: str) -> Path:
    """资产键 → 磁盘文件：去掉 <模组名>/ 前缀后相对模组根。"""
    parts = [p for p in key.split("/") if p]
    if parts and parts[0] == mod_name:
        parts = parts[1:]
    return root.joinpath(*parts).with_suffix(".png")


def rel_display(path: Path, repo_root: Path) -> str:
    try:
        return path.relative_to(repo_root).as_posix()
    except ValueError:
        return path.as_posix()


def main(argv: list[str] | None = None) -> int:
    default_root = Path(__file__).resolve().parent.parent / "ZenithReborn"
    parser = argparse.ArgumentParser(
        description="ZenithReborn 资产完整性静态检查（不启游戏、不编译）"
    )
    parser.add_argument(
        "--root",
        default=str(default_root),
        help="模组根目录（默认：脚本上级目录下的 ZenithReborn）",
    )
    parser.add_argument("--quiet", action="store_true", help="只打印汇总与缺口，不打印清单")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    if not root.is_dir():
        print("[资产检查] 模组根目录不存在：%s" % root, file=sys.stderr)
        return 2

    mod_name = root.name
    repo_root = root.parent
    files = collect_sources(root)

    parts: list[TypePart] = []
    for path in files:
        clean, masked = read_source(path)
        parts.extend(parse_types(masked, 0, len(clean), path, line_starts(clean)))

    merged: dict[str, ModClass] = {}
    for part in parts:
        cls = merged.get(part.key)
        if cls is None:
            merged[part.key] = ModClass(part)
        else:
            cls.merge(part)

    # 简单名索引（基类名不带命名空间时按简单名解析；重名取先出现者）
    index: dict[str, ModClass] = {}
    for cls in merged.values():
        index.setdefault(cls.name, cls)

    checked: list[ModClass] = []
    for cls in merged.values():
        if cls.abstract:
            continue
        if not inherits_textured(cls, index):
            continue
        checked.append(cls)
    checked.sort(key=lambda c: (c.name, str(c.decl_path)))

    own_override = 0
    inherited_override = 0
    file_hit: list[tuple[ModClass, Path]] = []
    gaps: list[tuple[ModClass, str, Path]] = []
    native_refs: list[tuple[str, str]] = []
    mod_refs: list[tuple[str, Path]] = []
    asset_warnings: list[tuple[ModClass, Path]] = []

    for cls in checked:
        ns = namespace_of(cls.decl_path, read_source(cls.decl_path)[1])
        key = expected_asset_key(ns, cls.name)
        target = asset_key_to_file(root, key, mod_name)

        resolved = override_source(cls, index)
        if resolved is not None:
            provider, (clean_expr, masked_expr) = resolved
            if provider.key == cls.key:
                own_override += 1
            else:
                inherited_override += 1
            # 三元表达式里的字符串字面量都当资产引用
            if "?" in masked_expr:
                for literal in string_literals(clean_expr):
                    if literal.startswith("Terraria/"):
                        native_refs.append((cls.name, literal))
                    elif literal.startswith(mod_name + "/"):
                        ref = asset_key_to_file(root, literal, mod_name)
                        mod_refs.append((cls.name, ref))
                        if not ref.is_file():
                            gaps.append((cls, literal, ref))
            continue

        if target.is_file():
            file_hit.append((cls, target))
            continue

        gaps.append((cls, key, target))

    # Assets/ 下同名 png 的误导警告（既无 override，又只能靠 Assets 图）
    if checked:
        assets_pngs: dict[str, Path] = {}
        for path in sorted(root.rglob("*.png")):
            rel = path.relative_to(root)
            if any(part in SKIP_DIRS for part in rel.parts[:-1]):
                continue
            parts_rel = rel.parts
            if parts_rel and parts_rel[0].lower() == "assets":
                assets_pngs.setdefault(path.stem, path)
        for cls in checked:
            if override_source(cls, index) is not None:
                continue
            hit = assets_pngs.get(cls.name)
            if hit is not None:
                asset_warnings.append((cls, hit))

    gap_count = len({c.key for c, _, _ in gaps})
    # 根目录（非 Assets/）里存在的同名 png：即使本类靠 override 通过，也统计出来便于对照
    root_pngs = {
        path.stem: path
        for path in sorted(root.glob("*.png"))
        if path.is_file()
    }
    print(
        "[资产检查] 待检查类型 %d 个；显式 override %d 个（本类 %d / 继承 %d）；同名贴图命中 %d 个；缺口 %d 个"
        % (
            len(checked),
            own_override + inherited_override,
            own_override,
            inherited_override,
            len(file_hit),
            gap_count,
        )
    )
    if root_pngs:
        names = {c.name for c in checked}
        shared = [stem for stem in sorted(root_pngs) if stem in names]
        if shared:
            print(
                "[资产检查] 模组根下同名贴图 %d 个：%s"
                % (len(shared), "、".join(stem + ".png" for stem in shared))
            )

    if not args.quiet:
        if file_hit:
            print("[资产检查] 靠同名贴图通过：")
            for cls, path in file_hit:
                print("  %s  → %s" % (cls.name, rel_display(path, repo_root)))
        if native_refs:
            print("[资产检查] 未验证的原版路径 %d 条（不校验存在性）：" % len(native_refs))
            for owner, literal in native_refs:
                print("  %s：%s" % (owner, literal))
        if mod_refs:
            print("[资产检查] override 显式引用的本模组资产 :")
            for owner, path in mod_refs:
                print("  %s：%s" % (owner, rel_display(path, repo_root)))
        if asset_warnings:
            print("[资产检查] 警告：以下类型没有 Texture override，只在 Assets/ 下有同名图")
            for cls, path in asset_warnings:
                print(
                    "  %s:%d  %s  %s  ← Assets 下的图不参与默认查找，等于没有"
                    % (
                        rel_display(cls.decl_path, repo_root),
                        cls.decl_line,
                        cls.name,
                        rel_display(path, repo_root),
                    )
                )

    if gaps:
        print("[资产检查] 缺口清单（文件:行号  类名  → 期望路径）：")
        for cls, key, _target in gaps:
            print(
                "  %s:%d  %s  → %s.png"
                % (rel_display(cls.decl_path, repo_root), cls.decl_line, cls.name, key)
            )
        print(
            "[资产检查] 结论：FAIL，上述类型缺贴图也没写 Texture override；"
            "客户端 AutoStaticDefaults 请求 Texture 时会抛 MissingResourceException"
        )
        return 1

    print("[资产检查] 结论：PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
