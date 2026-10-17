#!/usr/bin/env python3
"""주석만 바뀌었는지 확인한다. 코드가 한 줄이라도 달라지면 실패다.

    python3 comment-guard.py <기준>      예: HEAD, HEAD~1, origin/main

기준 커밋과 작업 트리를 견줘, 두 쪽에서 주석을 걷어낸 뒤 남은 줄을 맞춘다.
문자열 안의 `//` 같은 것까지 완벽히 가르지는 않지만(그러려면 파서가 필요하다)
**같은 규칙을 양쪽에 똑같이 적용**하므로 주석만 건드린 변경은 반드시 통과하고,
코드를 건드리면 거의 모든 경우에 걸린다.
"""
import re, subprocess, sys, difflib

def strip(src, ext):
    if ext == '.razor':
        src = re.sub(r'@\*.*?\*@', '', src, flags=re.S)
    elif ext == '.css':
        src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
    elif ext in ('.cs', '.dart'):
        src = re.sub(r'/\*.*?\*/', '', src, flags=re.S)
        src = '\n'.join(l for l in src.splitlines() if not l.lstrip().startswith('//'))
    return [l.rstrip() for l in src.splitlines() if l.strip()]

# 인자 하나면 「그 커밋 ↔ 작업 트리」, 둘이면 「커밋 ↔ 커밋」
base = sys.argv[1] if len(sys.argv) > 1 else 'HEAD'
head = sys.argv[2] if len(sys.argv) > 2 else None
changed = subprocess.run(['git', 'diff', '--name-only', base] + ([head] if head else []),
                         capture_output=True, text=True).stdout.split()

bad = ok = skipped = 0
for p in changed:
    ext = '.' + p.rsplit('.', 1)[-1] if '.' in p else ''
    if ext not in ('.razor', '.cs', '.css', '.dart'):
        skipped += 1
        continue
    old = subprocess.run(['git', 'show', f'{base}:{p}'], capture_output=True, text=True)
    if old.returncode:                      # 새로 만든 파일
        skipped += 1
        continue
    if head:
        cur = subprocess.run(['git', 'show', f'{head}:{p}'], capture_output=True, text=True)
        if cur.returncode:                  # 지운 파일
            skipped += 1
            continue
        new = cur.stdout
    else:
        try:
            new = open(p, encoding='utf-8').read()
        except FileNotFoundError:
            skipped += 1
            continue
    a, b = strip(old.stdout, ext), strip(new, ext)
    if a == b:
        ok += 1
        continue
    bad += 1
    print(f'\n✗ 코드가 바뀌었다 — {p}')
    for line in list(difflib.unified_diff(a, b, lineterm=''))[:20]:
        print('   ' + line)

print(f'\n주석만 바뀜 {ok}개 · 코드도 바뀜 {bad}개 · 건너뜀 {skipped}개')
sys.exit(1 if bad else 0)
