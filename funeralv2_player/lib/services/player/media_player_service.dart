import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

class MediaPlayerService {
  // MediaKit 관련 객체
  late final Player player = Player();
  late final VideoController videoController = VideoController(player);
  
  // 배경음악 재생용 MediaKit Player 객체
  late final Player _musicPlayer = Player();

  MediaPlayerService() {
    _setupHighQualityVideoOptions();
  }

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

  // 비디오 플레이어 초기화 및 재생
  Future<void> playVideo(String path, Function() onInitialized) async {
    try {
      // 반복 재생 설정
      await player.setPlaylistMode(PlaylistMode.loop);
      // isMuted 여부에 따라 초기 볼륨 설정 (비디오는 기본적으로 0.0이지만 명시적 처리)
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

  // 배경 음악 초기화 및 재생
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

  // 음악 볼륨/음소거 즉시 업데이트
  Future<void> updateMusicVolume(double volume, {bool isMuted = false}) async {
    final double vol = isMuted ? 0.0 : volume.clamp(0.0, 100.0);
    await _musicPlayer.setVolume(vol);
  }

  Future<void> stopVideo() async {
    try {
      await player.stop();
      // 비디오가 종료되었을 때 마지막 프레임이 잔상으로 유지되는 문제를 소거하기 위해 빈 미디어를 엽니다.
      await player.open(Media(''));
    } catch (_) {}
  }

  Future<void> stopMusic() async {
    await _musicPlayer.stop();
  }

  Future<void> dispose() async {
    await player.dispose();
    await _musicPlayer.dispose();
  }
}
