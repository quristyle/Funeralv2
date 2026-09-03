import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:funeralv2_player/services/auth/device_auth.dart';

/// [장비 인증 키 시험]
///
/// 핵심은 **키가 없을 때 아무것도 싣지 않는 것**이다 — 서버 검증(D-M1)이 꺼져 있는
/// 현장(지금 전부)에서 요청이 지금과 완전히 같아야 한다.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('키가 없으면 헤더도 주소도 그대로다', () async {
    SharedPreferences.setMockInitialValues({});
    await DeviceAuth.load();

    expect(DeviceAuth.headers(), isEmpty);
    expect(DeviceAuth.appendToUrl('http://s/api/funeral/hubs/device'),
        'http://s/api/funeral/hubs/device');
  });

  test('키가 있으면 헤더와 쿼리에 실린다', () async {
    SharedPreferences.setMockInitialValues({DeviceAuth.prefsKey: 'k-1'});
    await DeviceAuth.load();

    expect(DeviceAuth.headers(), {'X-Device-Token': 'k-1'});
    expect(DeviceAuth.appendToUrl('http://s/hub'), 'http://s/hub?deviceToken=k-1');
    expect(DeviceAuth.appendToUrl('http://s/hub?a=1'),
        'http://s/hub?a=1&deviceToken=k-1');
  });

  test('빈 값으로 저장하면 지운 것과 같다', () async {
    SharedPreferences.setMockInitialValues({DeviceAuth.prefsKey: 'old'});
    await DeviceAuth.load();
    await DeviceAuth.save('   ');

    expect(DeviceAuth.headers(), isEmpty);
    final prefs = await SharedPreferences.getInstance();
    expect(prefs.getString(DeviceAuth.prefsKey), isNull);
  });

  test('키에 든 특수문자는 쿼리에서 인코딩된다', () async {
    SharedPreferences.setMockInitialValues({});
    await DeviceAuth.load();
    await DeviceAuth.save('a b&c');

    expect(DeviceAuth.appendToUrl('http://s/hub'),
        'http://s/hub?deviceToken=a+b%26c');
  });
}
