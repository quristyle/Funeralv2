# 프로젝트관리 DB 이관 도구

ASIS(`jsini` / `projmng` 스키마) → TOBE(`projmng` / `projmng` 스키마).

무엇을 왜 이렇게 했는지는
[docs/analysis/35-projmng-db-tobe-migration.md](../../docs/analysis/35-projmng-db-tobe-migration.md) 에 있다.

## 준비

암호는 스크립트에 없다. 옆에 `dsn.env` 를 만들어 채운다(`.gitignore` 대상이다).

```bash
cp scripts/projmng-db-migration/dsn.env.example scripts/projmng-db-migration/dsn.env
```

파이썬 3 과 `psycopg2` 가 필요하다. `psql` · `pg_dump` 는 필요 없다 —
이 장비에 없어서 `pg_catalog` 를 직접 읽는 방식으로 짰다.

## 쓰는 법

```bash
python dump_asis.py          # ASIS 구조를 out/*.sql 로 뽑는다
python load_tobe.py --dry-run   # 무엇을 옮길지만 보여 준다
python load_tobe.py          # 실제로 옮긴다
python verify.py --deep      # ASIS·TOBE 대조 (내용 해시까지)
python smoke_tobe.py         # TOBE 프로시저를 실제로 불러 본다
python pmq.py "select * from dev_proj"   # ASIS 조회
```

## 안전장치

- **ASIS 는 read-only 트랜잭션으로만 연다.** 서버가 쓰기를 거부하므로
  실수로도 원본이 바뀌지 않는다. `pmq.py` 는 쓰기 문장을 보내기 전에 한 번 더 거른다.
- **`load_tobe.py` 는 전체가 트랜잭션 하나다.** 테이블마다 행수를 즉시 대조하고,
  하나라도 어긋나면 통째로 롤백한다.
- TOBE 에 이미 객체가 서 있으면 `--replace` 없이는 덮어쓰지 않는다.

## 다시 옮길 때

ASIS 가 계속 돌고 있어 자료는 계속 쌓인다. 전환 시점에 한 번 더 맞추면 된다.

```bash
python dump_asis.py && python load_tobe.py --replace && python verify.py --deep
```
