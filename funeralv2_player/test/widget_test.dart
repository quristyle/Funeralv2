import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:funeralv2_player/services/update/update_service.dart';
import 'package:funeralv2_player/widgets/update_dialog.dart';

/// [새 버전 확인 팝업 위젯 시험]
///
/// 원래 이 파일은 Flutter 템플릿 그대로였다 — 존재하지 않는 `MyApp` 과 카운터 화면을
/// 시험해서 `flutter test` 전체를 항상 실패시켰다(48번 문서 9절).
///
/// 여기서는 **네트워크 없이** 도는 것만 시험한다. `UpdateDialog` 에 확인 결과를
/// `initial` 로 주면 팝업이 조회를 다시 하지 않으므로, flutter_test 가 실제 HTTP 를
/// 막는 것과 무관하게 결정적으로 돈다.
void main() {
  UpdateCheck check({required bool hasUpdate, ReleaseAsset? asset}) {
    return UpdateCheck(
      currentVersion: '1.0.0',
      latestVersion: hasUpdate ? 'v9.9.9' : 'v1.0.0',
      target: const PlayerTarget(key: 'windows-x64', label: 'Windows x64'),
      asset: asset,
      releaseUrl: UpdateService.releasesPage,
      publishedAt: '2026-09-01T00:00:00Z',
      hasUpdate: hasUpdate,
    );
  }

  Future<void> pumpDialog(WidgetTester tester, UpdateCheck initial) async {
    await tester.pumpWidget(
      MaterialApp(home: UpdateDialog(initial: initial)),
    );
    await tester.pump();
  }

  testWidgets('최신 버전이면 그렇게 말하고 받기 단추가 없다', (tester) async {
    await pumpDialog(tester, check(hasUpdate: false));

    expect(find.text('프로그램 버전'), findsOneWidget);
    expect(find.text('최신 버전을 쓰고 있습니다'), findsOneWidget);
    expect(find.textContaining('설치 파일 받기'), findsNothing);
    expect(find.text('닫기'), findsOneWidget);
  });

  testWidgets('새 버전 + 자산이 있으면 받기 단추가 나온다', (tester) async {
    final asset = ReleaseAsset(
      name: 'funeralv2_player-9.9.9-windows-x64.zip',
      url: 'https://example.invalid/a.zip',
      size: 40 * 1024 * 1024,
    );
    await pumpDialog(tester, check(hasUpdate: true, asset: asset));

    expect(find.textContaining('새 버전 v9.9.9'), findsOneWidget);
    // 데스크톱(시험 환경)은 스스로 설치하지 못하므로 '받기' 문구다.
    expect(find.text('설치 파일 받기'), findsOneWidget);
    expect(find.textContaining('windows-x64.zip'), findsOneWidget);
    expect(find.textContaining('40.0 MB'), findsOneWidget);
  });

  testWidgets('새 버전인데 이 장비용 파일이 없으면 그렇게 알린다', (tester) async {
    await pumpDialog(tester, check(hasUpdate: true, asset: null));

    expect(
      find.text('새 버전이 있지만 이 장비용 파일이 릴리스에 없습니다'),
      findsOneWidget,
    );
    expect(find.text('설치 파일 받기'), findsNothing);
  });
}
