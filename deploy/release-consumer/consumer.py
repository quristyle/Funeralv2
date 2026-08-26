#!/usr/bin/env python3
"""배포 요청 큐 소비자 (참고 구현).

JSini 포털의 배포 도구(/portal/release)가 큐에 넣은 요청을 집어가 스크립트를 돌린다.

이 파일이 여기 있는 이유
------------------------
지금 배포 장비에서 도는 소비자는 **이 저장소 밖에 있다.** 그래서 포털과 배포
장비 사이의 약속(메시지 모양)이 반쪽만 버전 관리되고 있었다. 이 파일은 그
약속을 저장소 안에 적어 둔 것이다.

**이미 도는 소비자를 이 파일로 바꿀 필요는 없다.** 포털 설정의
``Release:Targets[].WrapperPath`` 에 ``release-run.sh`` 경로를 넣으면, 포털이
메시지의 ``script`` 자리에 래퍼를 넣어 보내므로 기존 소비자가 그대로 동작한다.
자세한 것은 README.md 를 읽는다.

메시지 모양
-----------
::

    {
      "script":  "/home/lee/projects/wrkScripts/release-run.sh",
      "args":    ["<runId>", "<token>", "<callbackUrl>", "/…/ReleaseRecept.sh", "Release", "Release"],
      "targetKey": "jin114",
      "runId":   "…",
      "wrapped": true,
      "callbackUrl": "http://…/api/auth/release/runs/…/events",
      "token":   "…",
      "targetScript": "/…/ReleaseRecept.sh"
    }

``script`` · ``args`` 두 값만 봐도 동작한다. 나머지는 새 소비자가 쓸 수 있게
함께 보내는 것이다.

안전에 관한 것
--------------
``ALLOWED_SCRIPTS`` 를 채우면 **메시지가 시키는 아무 경로나 실행하지 않는다.**
이 큐(``run_script``)는 헬프데스크의 메일 발송과 공유하고 있어서, 큐에 메시지를
넣을 수 있는 쪽이면 무엇이든 실행시킬 수 있는 상태다. 목록을 비워 두면 예전과
똑같이 동작하지만, 채워 두는 것을 권한다.

사용법::

    pip install pika
    RELEASE_QUEUE_HOST=localhost python3 consumer.py
"""

import json
import logging
import os
import subprocess
import sys

try:
    import pika
except ImportError:  # pragma: no cover
    sys.exit("pika 가 필요하다: pip install pika")

QUEUE_HOST = os.environ.get("RELEASE_QUEUE_HOST", "localhost")
QUEUE_NAME = os.environ.get("RELEASE_QUEUE_NAME", "run_script")

# 스크립트 하나가 이 시간을 넘기면 끊는다. 포털 쪽 TimeoutSeconds 와 맞춰 둔다.
RUN_TIMEOUT = int(os.environ.get("RELEASE_RUN_TIMEOUT", "1800"))

# 실행을 허용할 스크립트의 절대 경로.
#
# 비워 두면 무엇이든 실행한다(예전과 같은 동작). 채우면 목록에 없는 경로는 거절한다.
# 환경변수로 콜론으로 이어 준다: RELEASE_ALLOWED_SCRIPTS=/a/b.sh:/c/d.sh
ALLOWED_SCRIPTS = [
    p for p in os.environ.get("RELEASE_ALLOWED_SCRIPTS", "").split(":") if p
]

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(message)s",
)
log = logging.getLogger("release-consumer")


def handle(body: bytes) -> None:
    try:
        msg = json.loads(body.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        log.error("메시지를 읽을 수 없다: %s", exc)
        return

    script = msg.get("script")
    args = msg.get("args") or []
    run_id = msg.get("runId")

    if not script:
        log.error("script 가 없는 메시지다: %r", msg)
        return

    if not isinstance(args, list) or not all(isinstance(a, str) for a in args):
        log.error("args 가 문자열 배열이 아니다: %r", args)
        return

    if ALLOWED_SCRIPTS and script not in ALLOWED_SCRIPTS:
        # 목록에 없는 경로는 돌리지 않는다. 조용히 넘기지 않고 남겨 둔다.
        log.error("허용 목록에 없는 스크립트다. 실행하지 않는다: %s", script)
        return

    if not os.path.isfile(script):
        log.error("스크립트가 없다: %s", script)
        return

    log.info("실행: %s (run=%s, 인자 %d개)", script, run_id, len(args))

    # shell=False 다. 인자를 셸에 넘기지 않으므로 따옴표·공백을 걱정할 필요가 없다.
    #
    # 출력을 잡지 않고 그대로 흘려보낸다. 진행 상황 보고는 래퍼(release-run.sh)가
    # 하고, 여기서 또 잡으면 두 번 읽는 셈이 된다.
    try:
        rc = subprocess.call([script, *args], timeout=RUN_TIMEOUT)
        log.info("끝: %s rc=%s (run=%s)", script, rc, run_id)
    except subprocess.TimeoutExpired:
        log.error("제한 시간(%d초)을 넘겨 끊었다: %s (run=%s)",
                  RUN_TIMEOUT, script, run_id)
    except OSError as exc:
        log.error("실행하지 못했다: %s (%s)", script, exc)


def main() -> None:
    params = pika.ConnectionParameters(
        host=QUEUE_HOST,
        heartbeat=600,
        blocked_connection_timeout=300,
    )
    connection = pika.BlockingConnection(params)
    channel = connection.channel()

    # durable 은 포털의 Release:Durable 과 **반드시 같아야 한다.**
    # 다르면 브로커가 PRECONDITION_FAILED 를 낸다.
    channel.queue_declare(queue=QUEUE_NAME, durable=False)

    # 한 번에 하나만 집어간다. 배포 스크립트 둘이 같은 체크아웃에서 도는 것을 막는다.
    channel.basic_qos(prefetch_count=1)

    def on_message(ch, method, properties, body):
        try:
            handle(body)
        finally:
            # 무엇이 있어도 ack 한다. 실패한 요청을 큐에 되돌리면 같은 배포를
            # 무한히 재시도한다 — 배포는 재시도해도 좋은 일이 아니다.
            ch.basic_ack(delivery_tag=method.delivery_tag)

    channel.basic_consume(queue=QUEUE_NAME, on_message_callback=on_message)
    log.info("큐를 기다린다: %s @ %s", QUEUE_NAME, QUEUE_HOST)

    try:
        channel.start_consuming()
    except KeyboardInterrupt:
        log.info("멈춘다")
        channel.stop_consuming()
    finally:
        connection.close()


if __name__ == "__main__":
    main()
