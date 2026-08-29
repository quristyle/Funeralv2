"""접속 정보를 한 곳에서 읽는다.

암호를 스크립트에 적지 않는다. 저장소 규약대로 옆에 둔 `dsn.env`(.gitignore 대상)
또는 환경변수에서 읽는다. 없으면 무엇이 없는지 알려 주고 멈춘다.

  cp scripts/projmng-db-migration/dsn.env.example scripts/projmng-db-migration/dsn.env
  # 값을 채운 뒤
  python scripts/projmng-db-migration/verify.py
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ENV_FILE = os.path.join(HERE, "dsn.env")

SCHEMA = "projmng"


def _load_env_file():
    if not os.path.exists(ENV_FILE):
        return
    with open(ENV_FILE, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            # 이미 환경변수로 준 값이 있으면 그것을 우선한다.
            os.environ.setdefault(key.strip(), value.strip())


_load_env_file()


def dsn(prefix):
    """PROJMNG_ASIS_* / PROJMNG_TOBE_* 를 psycopg2 인자로 바꾼다."""
    need = ("HOST", "PORT", "DB", "USER", "PASSWORD")
    missing = [f"{prefix}_{n}" for n in need if not os.environ.get(f"{prefix}_{n}")]
    if missing:
        sys.exit(
            f"접속 정보가 없다: {', '.join(missing)}\n"
            f"  {ENV_FILE} 를 만들거나(dsn.env.example 참고) 환경변수로 준다."
        )
    return dict(
        host=os.environ[f"{prefix}_HOST"],
        port=int(os.environ[f"{prefix}_PORT"]),
        dbname=os.environ[f"{prefix}_DB"],
        user=os.environ[f"{prefix}_USER"],
        password=os.environ[f"{prefix}_PASSWORD"],
        connect_timeout=15,
    )


def asis():
    return dsn("PROJMNG_ASIS")


def tobe():
    return dsn("PROJMNG_TOBE")


def label(d):
    return f"{d['host']}:{d['port']}/{d['dbname']}"
