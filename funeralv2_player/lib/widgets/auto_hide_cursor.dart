import 'dart:async';
import 'package:flutter/material.dart';

/// [마우스 커서 자동 숨김]
///
/// 사이니지 화면에 마우스 포인터가 떠 있으면 안 된다.
/// 다만 현장에서 관리자가 설정 화면에 진입하려면 포인터가 필요하므로,
/// 완전히 없애지 않고 "움직이면 보이고, 멈추면 사라지는" 방식으로 처리한다.
///
/// cage(Wayland) 환경에서는 unclutter 같은 X11 도구를 쓸 수 없어서
/// 앱 자체가 커서 표시를 제어해야 한다. 이 위젯은 플랫폼과 무관하게 동작한다.
class AutoHideCursor extends StatefulWidget {
  final Widget child;

  /// 마지막 움직임 이후 이 시간이 지나면 커서를 숨긴다.
  final Duration idleTimeout;

  const AutoHideCursor({
    super.key,
    required this.child,
    this.idleTimeout = const Duration(seconds: 3),
  });

  @override
  State<AutoHideCursor> createState() => _AutoHideCursorState();
}

class _AutoHideCursorState extends State<AutoHideCursor> {
  /// 기동 직후에는 숨긴 상태로 시작한다. (사이니지가 켜지자마자 포인터가 보이면 안 된다)
  bool _visible = false;
  Timer? _hideTimer;

  /// [움직임 감지]
  /// 포인터가 움직이거나 눌리면 커서를 드러내고, 유휴 타이머를 다시 건다.
  void _onActivity() {
    _hideTimer?.cancel();

    if (!_visible) {
      setState(() => _visible = true);
    }

    _hideTimer = Timer(widget.idleTimeout, () {
      if (mounted && _visible) {
        setState(() => _visible = false);
      }
    });
  }

  @override
  void dispose() {
    _hideTimer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: _visible ? MouseCursor.defer : SystemMouseCursors.none,
      onHover: (_) => _onActivity(),
      // 터치 키오스크에서도 조작 직후에는 포인터가 보이도록 눌림도 활동으로 친다.
      child: Listener(
        onPointerDown: (_) => _onActivity(),
        onPointerMove: (_) => _onActivity(),
        child: widget.child,
      ),
    );
  }
}
