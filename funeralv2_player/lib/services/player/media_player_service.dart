import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:audioplayers/audioplayers.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

class MediaPlayerService {
  // MediaKit 관련 객체
  late final Player player = Player();
  late final VideoController videoController = VideoController(player);
  
  final AudioPlayer _audioPlayer = AudioPlayer();

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
      await _audioPlayer.stop();
      await _audioPlayer.setReleaseMode(ReleaseMode.loop);
      
      // 음소거 상태면 볼륨 0, 아니면 설정된 볼륨 적용
      final double vol = isMuted ? 0.0 : (volume / 100.0).clamp(0.0, 1.0);
      await _audioPlayer.setVolume(vol);

      if (kIsWeb || path.startsWith('http')) {
        await _audioPlayer.play(UrlSource(path));
      } else {
        await _audioPlayer.play(DeviceFileSource(path));
      }
      print('[Music] 재생 시작 (볼륨: $vol): $path');
    } catch (e) {
      print('[Music Error] 재생 실패: $e');
    }
  }

  // 음악 볼륨/음소거 즉시 업데이트
  Future<void> updateMusicVolume(double volume, {bool isMuted = false}) async {
    final double vol = isMuted ? 0.0 : (volume / 100.0).clamp(0.0, 1.0);
    await _audioPlayer.setVolume(vol);
  }

  Future<void> stopVideo() async {
    await player.stop();
  }

  Future<void> stopMusic() async {
    await _audioPlayer.stop();
  }

  Future<void> dispose() async {
    await player.dispose();
    await stopMusic();
    await _audioPlayer.dispose();
  }
}
