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
      // 볼륨 0 (배경음악과 중복 방지)
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
  Future<void> playMusic(String path, double volume) async {
    try {
      await _audioPlayer.stop();
      await _audioPlayer.setReleaseMode(ReleaseMode.loop);
      
      final double vol = (volume / 100.0).clamp(0.0, 1.0);
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
