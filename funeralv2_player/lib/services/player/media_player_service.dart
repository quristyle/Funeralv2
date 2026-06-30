import 'dart:io' as io;
import 'package:flutter/foundation.dart';
import 'package:audioplayers/audioplayers.dart';
import 'package:video_player/video_player.dart';

class MediaPlayerService {
  VideoPlayerController? videoController;
  final AudioPlayer _audioPlayer = AudioPlayer();

  // 비디오 플레이어 초기화 및 재생
  Future<void> playVideo(String path, Function() onInitialized) async {
    await stopVideo();

    try {
      if (kIsWeb || path.startsWith('http')) {
        // 웹이거나 URL인 경우
        videoController = VideoPlayerController.networkUrl(Uri.parse(path));
      } else {
        // 네이티브 파일인 경우
        final file = io.File(path);
        if (!await file.exists()) return;
        videoController = VideoPlayerController.file(file);
      }

      await videoController!.initialize();
      await videoController!.setLooping(true);
      await videoController!.setVolume(0); 
      await videoController!.play();
      onInitialized();
    } catch (e) {
      print('비디오 재생 실패: $e');
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
    } catch (e) {
      print('음악 재생 실패: $e');
    }
  }

  Future<void> stopVideo() async {
    if (videoController != null) {
      await videoController!.pause();
      await videoController!.dispose();
      videoController = null;
    }
  }

  Future<void> stopMusic() async {
    await _audioPlayer.stop();
  }

  Future<void> dispose() async {
    await stopVideo();
    await stopMusic();
    await _audioPlayer.dispose();
  }
}
