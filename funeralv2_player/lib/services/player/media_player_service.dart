import 'dart:io';
import 'package:audioplayers/audioplayers.dart';
import 'package:video_player/video_player.dart';

class MediaPlayerService {
  VideoPlayerController? videoController;
  final AudioPlayer _audioPlayer = AudioPlayer();

  // 비디오 플레이어 초기화 및 재생
  Future<void> playVideo(String localPath, Function() onInitialized) async {
    // 기존 비디오 해제
    await stopVideo();

    final file = File(localPath);
    if (!await file.exists()) {
      print('비디오 파일이 로컬 디스크에 존재하지 않습니다: $localPath');
      return;
    }

    try {
      videoController = VideoPlayerController.file(file);
      await videoController!.initialize();
      await videoController!.setLooping(true);
      await videoController!.setVolume(0); // 비디오는 무음 재생 (배경음악과 혼선 방지)
      await videoController!.play();
      onInitialized();
      print('로컬 비디오 무한 반복 재생 성공: $localPath');
    } catch (e) {
      print('비디오 플레이어 초기화 실패: $e');
    }
  }

  // 배경 음악 초기화 및 재생
  Future<void> playMusic(String localPath, double volume) async {
    try {
      await _audioPlayer.stop();
      await _audioPlayer.setReleaseMode(ReleaseMode.loop);
      
      // volume 범위: 0 ~ 100 ➔ 0.0 ~ 1.0 변환
      final double vol = (volume / 100.0).clamp(0.0, 1.0);
      await _audioPlayer.setVolume(vol);

      await _audioPlayer.play(DeviceFileSource(localPath));
      print('배경 음악 로컬 재생 개시 (볼륨: $vol): $localPath');
    } catch (e) {
      print('배경 음악 재생 실패: $e');
    }
  }

  // 비디오 정지
  Future<void> stopVideo() async {
    if (videoController != null) {
      await videoController!.pause();
      await videoController!.dispose();
      videoController = null;
    }
  }

  // 오디오 정지
  Future<void> stopMusic() async {
    await _audioPlayer.stop();
  }

  // 전체 해제
  Future<void> dispose() async {
    await stopVideo();
    await stopMusic();
    await _audioPlayer.dispose();
  }
}
