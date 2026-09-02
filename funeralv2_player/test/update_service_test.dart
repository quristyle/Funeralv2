import 'package:flutter_test/flutter_test.dart';
import 'package:funeralv2_player/services/update/update_service.dart';

/// [새 버전 판정 규칙 시험]
///
/// 여기서 시험하는 것은 **네트워크가 필요 없는 부분**이다 —
/// 버전 비교와 "이 기기가 받아야 하는 파일" 판정. 둘이 틀리면 조용히 잘못된다.
/// 버전 비교가 틀리면 새 버전을 못 보거나 없는 새 버전을 알리고,
/// 자산 판정이 틀리면 **라즈베리파이용 파일을 Ubuntu 에 주게 된다**(실행되지 않는다).
void main() {
  group('버전 비교', () {
    test('태그의 v 접두사를 무시한다', () {
      expect(UpdateService.compareVersions('v1.0.1', '1.0.0'), greaterThan(0));
      expect(UpdateService.compareVersions('1.0.1', 'v1.0.0'), greaterThan(0));
    });

    test('빌드 번호(+n)는 비교에서 뺀다', () {
      // 릴리스 태그에는 빌드 번호가 없고 앱 자신의 버전에는 붙어 있다.
      // 그대로 비교하면 같은 버전이 다르게 보여 "새 버전 있음" 이 계속 뜬다.
      expect(UpdateService.compareVersions('v1.0.0', '1.0.0+7'), 0);
    });

    test('자리 수가 달라도 비교된다', () {
      expect(UpdateService.compareVersions('1.1', '1.0.9'), greaterThan(0));
      expect(UpdateService.compareVersions('1.0', '1.0.0'), 0);
    });

    test('숫자로 비교한다 (문자열 비교가 아니다)', () {
      // 문자열로 비교하면 '10' < '9' 가 되어 새 버전을 못 본다.
      expect(UpdateService.compareVersions('1.10.0', '1.9.0'), greaterThan(0));
      expect(UpdateService.compareVersions('2.0.0', '10.0.0'), lessThan(0));
    });

    test('꼬리가 붙은 자리도 숫자만 읽는다', () {
      expect(UpdateService.compareVersions('1.2.3-rc1', '1.2.3'), 0);
    });

    test('알 수 없는 값은 0 으로 본다', () {
      expect(UpdateService.parseVersion(UpdateService.unknownVersion), [0, 0, 0]);
      expect(
          UpdateService.compareVersions('1.0.0', UpdateService.unknownVersion),
          greaterThan(0));
    });

    test('빈 값도 0 으로 읽힌다 (그래서 현재 버전을 빈 값으로 두지 않는다)', () {
      // 리눅스는 version.json 에서 버전을 읽는다. 그 파일을 못 읽으면 빈 값이 되고,
      // 빈 값을 그대로 비교하면 **항상 새 버전이 있는 것**이 된다.
      // currentVersion() 이 빈 값을 unknownVersion 으로 바꾸는 이유다.
      expect(UpdateService.parseVersion(''), [0, 0, 0]);
      expect(UpdateService.compareVersions('1.0.0', ''), greaterThan(0));
    });
  });

  group('이 기기에 맞는 설치 파일 판정', () {
    // v1.0.0 릴리스에 실제로 붙어 있는 자산 목록.
    final assets = [
      _asset('funeralv2-player_1.0.0_debian13_arm64.deb'),
      _asset('funeralv2-player_1.0.0_ubuntu24_amd64.deb'),
      _asset('funeralv2-player_1.0.0_ubuntu24_arm64.deb'),
      _asset('funeralv2_player-1.0.0-android-releasesigned.apk'),
      _asset('funeralv2_player-1.0.0-debian13-arm64.tar.gz'),
      _asset('funeralv2_player-1.0.0-ubuntu24-amd64.tar.gz'),
      _asset('funeralv2_player-1.0.0-ubuntu24-arm64.tar.gz'),
      _asset('funeralv2_player-1.0.0-windows-x64.zip'),
      _asset('SHA256SUMS.txt'),
    ];

    test('윈도우는 zip 하나만 고른다', () {
      const target = PlayerTarget(key: 'windows-x64', label: 'Windows x64');
      expect(target.pick(assets)!.name, 'funeralv2_player-1.0.0-windows-x64.zip');
    });

    test('안드로이드는 apk 를 고른다', () {
      const target = PlayerTarget(key: 'android', label: 'Android');
      expect(target.pick(assets)!.name,
          'funeralv2_player-1.0.0-android-releasesigned.apk');
    });

    test('APK 가 둘이면 릴리스 키로 서명된 것을 먼저 쓴다', () {
      // 디버그 키 서명은 빌드마다 서명이 달라 덮어쓰기 업데이트가 거부된다.
      const target = PlayerTarget(key: 'android', label: 'Android');
      final both = [
        _asset('funeralv2_player-1.0.0-android-debugsigned.apk'),
        _asset('funeralv2_player-1.0.0-android-releasesigned.apk'),
      ];
      expect(target.pick(both)!.name,
          'funeralv2_player-1.0.0-android-releasesigned.apk');
    });

    test('라즈베리파이는 debian13 arm64 .deb 를 고른다', () {
      const target = PlayerTarget(
          key: 'debian13-arm64',
          label: 'Linux',
          distro: 'debian13',
          arch: 'arm64');
      expect(target.pick(assets)!.name,
          'funeralv2-player_1.0.0_debian13_arm64.deb');
    });

    test('Ubuntu arm64 는 라즈베리파이 파일을 집지 않는다', () {
      // 이게 이 판정의 핵심이다. `.deb` 로만 고르면 둘 중 아무 것이나 집어 오고,
      // debian13(glibc 2.41) 파일은 Ubuntu 24.04(2.39) 에서 실행되지 않는다.
      const target = PlayerTarget(
          key: 'ubuntu24-arm64',
          label: 'Linux',
          distro: 'ubuntu24',
          arch: 'arm64');
      expect(target.pick(assets)!.name,
          'funeralv2-player_1.0.0_ubuntu24_arm64.deb');
    });

    test('Ubuntu x64 는 amd64 를 고른다', () {
      const target = PlayerTarget(
          key: 'ubuntu24-amd64',
          label: 'Linux',
          distro: 'ubuntu24',
          arch: 'amd64');
      expect(target.pick(assets)!.name,
          'funeralv2-player_1.0.0_ubuntu24_amd64.deb');
    });

    test('리눅스는 tar.gz 를 고르지 않는다 (수동 설치용이다)', () {
      const target = PlayerTarget(
          key: 'debian13-arm64',
          label: 'Linux',
          distro: 'debian13',
          arch: 'arm64');
      final onlyTar = [_asset('funeralv2_player-1.0.0-debian13-arm64.tar.gz')];
      expect(target.pick(onlyTar), isNull);
    });

    test('맞는 자산이 없으면 null 이다', () {
      const target = PlayerTarget(key: 'windows-x64', label: 'Windows x64');
      expect(target.pick([_asset('SHA256SUMS.txt')]), isNull);
      expect(const PlayerTarget(key: 'unsupported', label: 'macos').pick(assets),
          isNull);
    });
  });

  group('확인 결과', () {
    const target = PlayerTarget(key: 'windows-x64', label: 'Windows x64');

    test('실패한 결과는 failed 이고 새 버전이 있다고 하지 않는다', () {
      final r = UpdateCheck.failed('1.0.0', target, '접속 실패');
      expect(r.failed, isTrue);
      expect(r.hasUpdate, isFalse);
      expect(r.error, '접속 실패');
    });

    test('현재 버전을 모르면 currentUnknown 이다', () {
      final r = UpdateCheck.failed(UpdateService.unknownVersion, target, 'x');
      expect(r.currentUnknown, isTrue);
    });
  });

  group('자산 크기 표기', () {
    test('1MB 이상은 MB, 그 아래는 KB', () {
      expect(ReleaseAsset(name: 'a', url: '', size: 40065853).sizeText, '38.2 MB');
      expect(ReleaseAsset(name: 'a', url: '', size: 877).sizeText, '1 KB');
    });
  });
}

ReleaseAsset _asset(String name) => ReleaseAsset(
      name: name,
      url: 'https://github.com/quristyle/Funeralv2/releases/download/v1.0.0/$name',
      size: 1024 * 1024,
    );
