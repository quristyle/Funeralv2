import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

/// [미디어 플레이어 서비스]
/// 장례식장 사이니지 전용 비디오 백그라운드 루프 재생 및 백그라운드 추모곡(오디오) 재생을 관리하는 서비스입니다.
/// 데스크톱 환경에서는 mpv(media_kit) 엔진의 하드웨어 가속 옵션을 고해상도로 셋업하여 고품질 렌더링을 제공합니다.
class MediaPlayerService {
  // 비디오 재생용 MediaKit Player 및 렌더 제어용 비디오 컨트롤러
  late final Player player = Player();
  late final VideoController videoController = VideoController(player);
  
  // 배경음악(BGM) 재생용 전용 오디오 Player 객체
  late final Player _musicPlayer = Player();

  /// [생성자]
  /// 서비스 인스턴스 생성 시 고품질 화면을 위한 비디오 디코딩 드라이버 설정을 시작합니다.
  MediaPlayerService() {
    _setupHighQualityVideoOptions();
  }

  /// [고품질 비디오 드라이버 옵션 설정]
  /// 데스크톱(Windows, Linux) 환경에서 실행될 때 mpv 내부 프로퍼티를 주입합니다.
  /// 하드웨어 가속 강제, 고품질 스케일러(spline36) 세팅, 수직 동기화 매칭을 진행하여
  /// 프레임이 튀거나 깜빡거리는 현상을 미연에 방지합니다.
  Future<void> _setupHighQualityVideoOptions() async {
    if (!kIsWeb) {
      try {
        final platform = player.platform as dynamic;
        // 하드웨어 가속 강제 (Windows: auto-safe 또는 d3d11va)
        await platform.setProperty('hwdec', 'auto-safe');
        // 고품질 스케일러 고정 (초기 로딩 시 bilinear로 흐려지는 현상 방지)
        await platform.setProperty('scale', 'spline36');
        await platform.setProperty('cscale', 'spline36');
        // 수직동기화 맞추어 프레임 지터/클램핑 경고 개선 및 루프 튐 방지
        await platform.setProperty('video-sync', 'display-resample');
        // 정밀 탐색 적용
        await platform.setProperty('hr-seek', 'yes');
        print('[Video] mpv 고화질 및 하드웨어 디코딩 속성 설정 완료');
      } catch (e) {
        print('[Video] mpv 속성 설정 중 에러: $e');
      }
    }
  }

  /// [비디오 재생 시작 및 제어]
  /// 입력받은 비디오 파일의 로컬 경로 혹은 웹 주소([path])를 열어 루프 모드로 재생합니다.
  /// 비디오가 준비되어 첫 프레임을 렌더링하기 시작하면 [onInitialized] 콜백을 실행합니다.
  Future<void> playVideo(String path, Function() onInitialized) async {
    try {
      // 반복 재생(무한 루프) 설정
      await player.setPlaylistMode(PlaylistMode.loop);
      // 백그라운드 영상이므로 사운드가 충돌하지 않도록 비디오 음소거 처리
      await player.setVolume(0.0);

      if (kIsWeb || path.startsWith('http')) {
        await player.open(Media(path));
      } else {
        final file = io.File(path);
        if (!await file.exists()) {
          print('[Video] 파일이 존재하지 않음: $path');
          return;
        }
        await player.open(Media(file.path));
      }
      
      onInitialized();
      print('[Video] MediaKit 재생 시작: $path');
    } catch (e) {
      print('[Video Error] MediaKit 초기화 실패: $e');
    }
  }

  /// [배경음악 재생 시작 및 제어]
  /// 특정 로컬 경로 또는 스트리밍 주소([path])의 음원 리소스를 로드하여 무한 루프로 백그라운드 재생합니다.
  /// 음소거 여부([isMuted]) 및 기본 볼륨 값([volume])을 적용하며, 볼륨은 0부터 100 사이로 자동 클램핑됩니다.
  Future<void> playMusic(String path, double volume, {bool isMuted = false}) async {
    try {
      await _musicPlayer.stop();
      await _musicPlayer.setPlaylistMode(PlaylistMode.loop);
      
      // 음소거 상태면 볼륨 0, 아니면 설정된 볼륨 적용
      // media_kit의 볼륨 단위는 0.0 ~ 100.0 입니다.
      final double vol = isMuted ? 0.0 : volume.clamp(0.0, 100.0);
      await _musicPlayer.setVolume(vol);

      if (kIsWeb || path.startsWith('http')) {
        await _musicPlayer.open(Media(path));
      } else {
        final file = io.File(path);
        if (!await file.exists()) {
          print('[Music] 파일이 존재하지 않음: $path');
          return;
        }
        await _musicPlayer.open(Media(file.path));
      }
      print('[Music] 재생 시작 (볼륨: $vol): $path');
    } catch (e) {
      print('[Music Error] 재생 실패: $e');
    }
  }

  /// [배경음악 음량 및 상태 동적 변경]
  /// 재생 중인 백그라운드 오디오의 음량을 실시간으로 조절합니다.
  Future<void> updateMusicVolume(double volume, {bool isMuted = false}) async {
    final double vol = isMuted ? 0.0 : volume.clamp(0.0, 100.0);
    await _musicPlayer.setVolume(vol);
  }

  /// [비디오 재생 정지]
  /// 재생을 중지하고 마지막 프레임이 화면에 잔상으로 얹혀 있지 않도록 빈 비디어를 강제 로드하여 소거합니다.
  Future<void> stopVideo() async {
    try {
      await player.stop();
      // 비디오가 종료되었을 때 마지막 프레임이 잔상으로 유지되는 문제를 소거하기 위해 빈 미디어를 엽니다.
      await player.open(Media(''));
    } catch (_) {}
  }

  /// [배경음악 재생 정지]
  Future<void> stopMusic() async {
    await _musicPlayer.stop();
  }

  /// [자원 해제]
  /// 앱 종료 또는 화면 디스패치 변경 시 사용하던 미디어 리소스 및 플레이어 세션을 메모리에서 해제합니다.
  Future<void> dispose() async {
    await player.dispose();
    await _musicPlayer.dispose();
  }
}
