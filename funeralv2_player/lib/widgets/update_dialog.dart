import 'dart:io';

import 'package:flutter/material.dart';

import '../services/update/update_service.dart';

/// [새 버전 확인 팝업]
///
/// 환경 설정 화면 머리줄의 아이콘으로 연다.
///
/// **설정 카드 안에 넣지 않고 팝업으로 뺐다.** 설정 화면은 저해상도 사이니지 패널에서
/// 세로 스크롤 없이 한 화면에 담는 것을 전제로 짜여 있어서(준수사항 4), 카드에 줄을
/// 더하면 720p 세로 모드에서 넘친다. 팝업은 카드 높이를 건드리지 않고,
/// 진행률·파일 이름·안내 문구를 놓을 자리도 넉넉하다.
///
/// 팝업 드래그(준수사항 3)는 여기서는 대상이 아니다 — 전체화면 키오스크에 창이
/// 하나뿐이고 마우스가 없는 장비가 많다. 그 규칙의 예외 조항(전체화면)과 같은 이유다.
class UpdateDialog extends StatefulWidget {
  const UpdateDialog({super.key, this.initial});

  /// 설정 화면이 이미 확인해 둔 결과가 있으면 그것으로 시작한다.
  /// 팝업을 열자마자 같은 요청을 또 보내지 않기 위한 것이다.
  final UpdateCheck? initial;

  /// 팝업을 띄운다. 화면 회전([quarterTurns]) 을 그대로 물려받아,
  /// 세로로 세운 패널에서도 글자가 바로 서게 한다.
  static Future<void> show(
    BuildContext context, {
    UpdateCheck? initial,
    int quarterTurns = 0,
  }) {
    return showDialog<void>(
      context: context,
      barrierColor: Colors.black87,
      builder: (_) => RotatedBox(
        quarterTurns: quarterTurns,
        child: UpdateDialog(initial: initial),
      ),
    );
  }

  @override
  State<UpdateDialog> createState() => _UpdateDialogState();
}

/// 팝업이 지금 무엇을 하고 있는지
enum _Phase { checking, result, downloading, downloaded, working, failed }

class _UpdateDialogState extends State<UpdateDialog> {
  static const Color _gold = Color(0xFFC0A060);

  _Phase _phase = _Phase.checking;
  UpdateCheck? _check;

  /// 내려받기 진행률 (0.0 ~ 1.0). 전체 크기를 모르면 null.
  double? _progress;
  int _received = 0;

  /// 내려받아 둔 파일
  File? _file;

  /// 화면 아래에 보여 줄 안내 · 오류 문구
  String? _message;

  @override
  void initState() {
    super.initState();
    if (widget.initial != null && !widget.initial!.failed) {
      _check = widget.initial;
      _phase = _Phase.result;
    } else {
      _runCheck();
    }
  }

  Future<void> _runCheck() async {
    setState(() {
      _phase = _Phase.checking;
      _message = null;
    });
    final result = await UpdateService.check();
    if (!mounted) return;
    setState(() {
      _check = result;
      _phase = result.failed ? _Phase.failed : _Phase.result;
      _message = result.error;
    });
  }

  Future<void> _download() async {
    final asset = _check?.asset;
    if (asset == null) return;

    setState(() {
      _phase = _Phase.downloading;
      _progress = null;
      _received = 0;
      _message = null;
    });

    try {
      final file = await UpdateService.download(
        asset,
        onProgress: (received, total) {
          if (!mounted) return;
          setState(() {
            _received = received;
            _progress = total > 0 ? received / total : null;
          });
        },
      );
      if (!mounted) return;
      setState(() {
        _file = file;
        _phase = _Phase.downloaded;
      });
      // 안드로이드는 받은 즉시 시스템 설치 화면으로 넘긴다.
      if (UpdateService.canInstallInPlace) await _install();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _phase = _Phase.failed;
        _message = '내려받기에 실패했습니다. ($e)';
      });
    }
  }

  Future<void> _install() async {
    final file = _file;
    if (file == null) return;

    setState(() {
      _phase = _Phase.working;
      _message = null;
    });

    try {
      // "알 수 없는 앱 설치" 가 허용되지 않았으면 그 설정 화면으로 안내한다.
      // 이걸 건너뛰면 설치 화면이 뜨지 않고 아무 일도 안 일어난 것처럼 보인다.
      if (!await UpdateService.installAllowed()) {
        if (!mounted) return;
        setState(() {
          _phase = _Phase.downloaded;
          _message = '이 앱에 "알 수 없는 앱 설치" 가 허용되어 있지 않습니다.\n'
              '아래 단추로 설정 화면을 열어 허용한 뒤 다시 설치를 누르세요.';
        });
        return;
      }

      await UpdateService.installApk(file);
      if (!mounted) return;
      setState(() {
        _phase = _Phase.downloaded;
        _message = '설치 화면을 띄웠습니다. 화면의 확인을 눌러 주세요.\n'
            '(TV 박스는 리모컨으로 누릅니다)';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _phase = _Phase.downloaded;
        _message = '설치 화면을 띄우지 못했습니다. ($e)';
      });
    }
  }

  /// [윈도우 제자리 교체 (D-P1)]
  /// 도우미를 띄우고 앱이 종료된다. 실패 시 도우미가 옛 버전으로 되돌린다.
  Future<void> _replaceWindows() async {
    final file = _file;
    if (file == null) return;

    if (!await UpdateService.canReplaceInPlace()) {
      if (!mounted) return;
      setState(() {
        _message = '설치 폴더에 쓸 수 없습니다 (관리자 권한이 필요한 폴더).\n'
            '아래 안내대로 수동으로 교체해 주세요.';
      });
      return;
    }

    if (!mounted) return;
    setState(() {
      _phase = _Phase.working;
      _message = '교체를 시작합니다. 프로그램이 곧 종료되고 새 버전으로 다시 시작됩니다.\n'
          '(실패하면 자동으로 이전 버전으로 되돌아옵니다 — 기록: %TEMP%\\jsini_player_update.log)';
    });
    // 사람이 위 문구를 읽을 시간을 준 뒤 교체를 시작한다. 이 호출은 돌아오지 않는다.
    await Future.delayed(const Duration(seconds: 2));
    await UpdateService.installWindowsUpdate(file);
  }

  /// [리눅스 설치 (D-P2)]
  Future<void> _installLinux() async {
    final file = _file;
    if (file == null) return;

    setState(() {
      _phase = _Phase.working;
      _message = null;
    });

    final fail = await UpdateService.installLinuxUpdate(file);
    if (!mounted) return;
    setState(() {
      _phase = _Phase.downloaded;
      _message = fail ??
          '설치가 시작되었습니다. 잠시 후 프로그램이 새 버전으로 다시 시작됩니다.\n'
              '(기록: journalctl -u funeralv2-player-update)';
    });
  }

  // ── 화면 ────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    final check = _check;
    final String latestText;
    if (_phase == _Phase.checking) {
      latestText = '확인 중...';
    } else if (check == null || check.latestVersion.isEmpty) {
      latestText = '-';
    } else {
      latestText = check.latestVersion;
    }

    return AlertDialog(
      backgroundColor: const Color(0xFF141414),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      titlePadding: const EdgeInsets.fromLTRB(20, 16, 12, 0),
      contentPadding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
      title: Row(
        children: [
          const Icon(Icons.system_update, color: _gold, size: 20),
          const SizedBox(width: 10),
          const Expanded(
            child: Text(
              '프로그램 버전',
              style: TextStyle(
                  color: Colors.white,
                  fontSize: 16,
                  fontWeight: FontWeight.bold),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.refresh, size: 18, color: Colors.white54),
            tooltip: '다시 확인',
            onPressed: _phase == _Phase.checking ||
                    _phase == _Phase.downloading ||
                    _phase == _Phase.working
                ? null
                : _runCheck,
          ),
        ],
      ),
      content: SizedBox(
        width: 420,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _buildStatusBar(),
            const SizedBox(height: 14),
            _buildRow('현재 버전', check?.currentVersion ?? '확인 중...'),
            _buildRow('최신 버전', latestText),
            _buildRow('이 장비', check?.target.label ?? '-'),
            if (check?.asset != null)
              _buildRow(
                '설치 파일',
                '${check!.asset!.name}  (${check.asset!.sizeText})',
                mono: true,
              ),
            if (_phase == _Phase.downloading) ...[
              const SizedBox(height: 12),
              LinearProgressIndicator(
                value: _progress,
                minHeight: 6,
                backgroundColor: Colors.white12,
                valueColor: const AlwaysStoppedAnimation(_gold),
              ),
              const SizedBox(height: 6),
              Text(
                _progress == null
                    ? '${(_received / 1024 / 1024).toStringAsFixed(1)} MB 받는 중...'
                    : '${(_progress! * 100).toStringAsFixed(0)} %  '
                        '(${(_received / 1024 / 1024).toStringAsFixed(1)} MB)',
                style: const TextStyle(color: Colors.white54, fontSize: 12),
              ),
            ],
            if (_message != null) ...[
              const SizedBox(height: 12),
              Text(
                _message!,
                style: const TextStyle(color: Colors.white54, fontSize: 12, height: 1.5),
              ),
            ],
            if (_phase == _Phase.downloaded && !UpdateService.canInstallInPlace)
              _buildManualGuide(),
            const SizedBox(height: 4),
          ],
        ),
      ),
      actions: _buildActions(),
    );
  }

  /// 위쪽 상태 띠. 설정 화면의 서버 연결 상태 띠와 같은 모양으로 맞췄다.
  Widget _buildStatusBar() {
    final (Color color, IconData icon, String text) = switch (_phase) {
      _Phase.checking => (Colors.orangeAccent, Icons.sync, '최신 버전을 확인하는 중...'),
      _Phase.failed => (Colors.redAccent, Icons.error, _check?.error ?? '확인 실패'),
      _Phase.downloading => (_gold, Icons.download, '설치 파일을 받는 중...'),
      _Phase.working => (_gold, Icons.settings, '설치 화면을 준비하는 중...'),
      _Phase.downloaded => (Colors.greenAccent, Icons.check_circle, '설치 파일을 받았습니다'),
      _Phase.result when _check!.currentUnknown => (
          Colors.orangeAccent,
          Icons.help,
          '현재 버전을 읽지 못했습니다 (최신은 ${_check!.latestVersion})'
        ),
      _Phase.result when _check!.missingAsset => (
          Colors.orangeAccent,
          Icons.help,
          '새 버전이 있지만 이 장비용 파일이 릴리스에 없습니다'
        ),
      _Phase.result when _check!.hasUpdate => (
          _gold,
          Icons.new_releases,
          '새 버전 ${_check!.latestVersion} 이 있습니다'
        ),
      _Phase.result => (Colors.greenAccent, Icons.check_circle, '최신 버전을 쓰고 있습니다'),
    };

    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
      decoration: BoxDecoration(
        color: color.withOpacity(0.08),
        borderRadius: BorderRadius.circular(6),
        border: Border.all(color: color.withOpacity(0.2)),
      ),
      child: Row(
        children: [
          Icon(icon, color: color, size: 18),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              style: TextStyle(color: color, fontSize: 13, fontWeight: FontWeight.bold),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildRow(String label, String value, {bool mono = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 76,
            child: Text(label,
                style: const TextStyle(color: Colors.white38, fontSize: 13)),
          ),
          Expanded(
            child: Text(
              value,
              style: TextStyle(
                color: Colors.white70,
                fontSize: mono ? 11 : 13,
                fontWeight: FontWeight.bold,
                fontFamily: mono ? 'Consolas' : null,
              ),
            ),
          ),
        ],
      ),
    );
  }

  /// 데스크톱에서 받은 뒤 사람이 해야 할 일.
  ///
  /// 앱이 스스로 교체하지 않는다 — 돌고 있는 실행 파일을 자기가 덮어쓸 수 없고,
  /// `.deb` 설치에는 root 가 필요하다. 그래서 **어디에 받았는지**를 정확히 알려 준다.
  Widget _buildManualGuide() {
    final path = _file?.path ?? '';
    final isDeb = path.endsWith('.deb');
    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('받은 곳',
              style: TextStyle(color: _gold, fontSize: 12, fontWeight: FontWeight.bold)),
          const SizedBox(height: 4),
          SelectableText(
            path,
            style: const TextStyle(
                color: Colors.white70, fontSize: 11, fontFamily: 'Consolas'),
          ),
          const SizedBox(height: 8),
          Text(
            isDeb
                ? '설치: sudo apt install ./${path.split(Platform.pathSeparator).last}\n'
                    '설치 후 sudo systemctl restart funeral-player 또는 재부팅'
                : '압축을 풀어 지금 실행 중인 폴더의 파일을 덮어쓴 뒤 다시 실행합니다.\n'
                    '(실행 중에는 덮어쓸 수 없으므로 프로그램을 먼저 종료합니다)',
            style: const TextStyle(color: Colors.white38, fontSize: 11, height: 1.5),
          ),
        ],
      ),
    );
  }

  List<Widget> _buildActions() {
    final busy = _phase == _Phase.checking ||
        _phase == _Phase.downloading ||
        _phase == _Phase.working;

    return [
      TextButton(
        onPressed: busy ? null : () => Navigator.of(context).pop(),
        style: TextButton.styleFrom(foregroundColor: Colors.white54),
        child: const Text('닫기'),
      ),
      // 안드로이드에서 설치 허용이 막혀 있을 때만 나오는 단추
      if (_phase == _Phase.downloaded &&
          UpdateService.canInstallInPlace &&
          (_message ?? '').contains('알 수 없는 앱 설치'))
        TextButton(
          onPressed: UpdateService.openInstallSettings,
          style: TextButton.styleFrom(foregroundColor: _gold),
          child: const Text('설치 허용 설정 열기'),
        ),
      if (_phase == _Phase.downloaded && UpdateService.canInstallInPlace)
        ElevatedButton(
          onPressed: _install,
          style: ElevatedButton.styleFrom(
              backgroundColor: _gold, foregroundColor: Colors.black),
          child: const Text('설치', style: TextStyle(fontWeight: FontWeight.bold)),
        ),
      // 윈도우: 도우미가 종료 대기 → 백업 → 교체 → 재기동 → 실패 시 되돌림 (D-P1)
      if (_phase == _Phase.downloaded && Platform.isWindows)
        ElevatedButton(
          onPressed: _replaceWindows,
          style: ElevatedButton.styleFrom(
              backgroundColor: _gold, foregroundColor: Colors.black),
          child: const Text('지금 교체 (앱이 잠시 종료됩니다)',
              style: TextStyle(fontWeight: FontWeight.bold)),
        ),
      // 리눅스: sudoers 로 허용된 도우미가 설치하고 systemd 가 재시작 (D-P2)
      if (_phase == _Phase.downloaded && Platform.isLinux)
        ElevatedButton(
          onPressed: _installLinux,
          style: ElevatedButton.styleFrom(
              backgroundColor: _gold, foregroundColor: Colors.black),
          child: const Text('지금 설치 (곧 재시작됩니다)',
              style: TextStyle(fontWeight: FontWeight.bold)),
        ),
      // 현재 버전을 못 읽은 경우에도 받을 수는 있게 둔다 — 사람이 보고 판단한다.
      if (_phase == _Phase.result &&
          (_check!.hasUpdate || _check!.currentUnknown) &&
          _check!.asset != null)
        ElevatedButton(
          onPressed: _download,
          style: ElevatedButton.styleFrom(
              backgroundColor: _gold, foregroundColor: Colors.black),
          child: Text(
            UpdateService.canInstallInPlace ? '받아서 설치' : '설치 파일 받기',
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
        ),
    ];
  }
}
