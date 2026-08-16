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
  late final VideoController videoController = VideoController(
    player,
    configuration: VideoControllerConfiguration(
      enableHardwareAcceleration: _useHardwareVideoRendering,
    ),
  );

  /// [비디오 하드웨어 렌더링 사용 여부]
  ///
  /// 라즈베리파이(V3D 드라이버)에서는 media_kit 의 H/W 렌더링 경로가 정상 동작하지 않는다.
  ///  - media_kit_video 1.3.1: GL 텍스처를 dmabuf 로 export 하지 못해
  ///    (MESA: Failed to export gem bo ... to dmabuf) 영상 자리가 빈 화면으로 남는다.
  ///  - media_kit_video 2.0.1: 영상은 나오지만 GPU 동기화 fd(sync_file)가 초당 15개씩 누수되어
  ///    1분 만에 프로세스 fd 한도(1024)를 소진한다. 이후 소켓을 못 열어 API/SignalR 이 모두 끊긴다.
  ///
  /// 그래서 Linux 는 기본값을 S/W 렌더링으로 둔다. CPU 사용률은 올라가지만(Pi 4 기준 약 310%)
  /// 24시간 연속 구동에서 유일하게 안정적인 조합이다.
  /// GPU 렌더링이 정상 동작하는 Linux 장비(x86 데스크톱 등)에서는
  /// 환경변수 PLAYER_VIDEO_HWACCEL=1 로 켤 수 있다. Windows/macOS 는 영향받지 않는다.
  static bool get _useHardwareVideoRendering {
    if (kIsWeb || !io.Platform.isLinux) return true;
    final flag = io.Platform.environment['PLAYER_VIDEO_HWACCEL']?.toLowerCase();
    return flag == '1' || flag == 'true';
  }
  
  // 배경음악(BGM) 재생용 전용 오디오 Player 객체
  late final Player _musicPlayer = Player();

  // 현재 로드되어 재생 중인 소스 경로 추적 (동일 소스 재오픈으로 인한 재시작/깜빡임 방지용)
  String? _currentVideoPath;
  String? _currentMusicPath;

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
      // [동일 소스 가드] 이미 같은 영상이 로드되어 재생 중이면 재오픈하지 않습니다.
      // 설정 변경(SignalR) 시 영상 외 속성만 바뀌어도 재생이 처음부터 다시 시작되어 깜빡이는 현상을 방지합니다.
      if (_currentVideoPath == path) {
        onInitialized(); // 첫 프레임 렌더 콜백만 보장하고 재시작은 생략
        return;
      }

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

      _currentVideoPath = path; // 현재 로드된 영상 경로 기록
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
      // [동일 소스 가드] 이미 같은 음원이 재생 중이면 재오픈하지 않고 볼륨/음소거만 갱신합니다.
      // 음원 외 속성 변경 시 BGM이 처음부터 다시 재생되는 현상을 방지합니다.
      if (_currentMusicPath == path) {
        await updateMusicVolume(volume, isMuted: isMuted);
        return;
      }

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

      _currentMusicPath = path; // 현재 로드된 음원 경로 기록
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
      _currentVideoPath = null; // 현재 영상 경로 기록 해제 (다음 재생 시 정상 로드되도록)
      await player.stop();
      // 비디오가 종료되었을 때 마지막 프레임이 잔상으로 유지되는 문제를 소거하기 위해 빈 미디어를 엽니다.
      await player.open(Media(''));
    } catch (_) {}
  }

  /// [배경음악 재생 정지]
  Future<void> stopMusic() async {
    _currentMusicPath = null; // 현재 음원 경로 기록 해제 (다음 재생 시 정상 로드되도록)
    await _musicPlayer.stop();
  }

  /// [자원 해제]
  /// 앱 종료 또는 화면 디스패치 변경 시 사용하던 미디어 리소스 및 플레이어 세션을 메모리에서 해제합니다.
  Future<void> dispose() async {
    await player.dispose();
    await _musicPlayer.dispose();
  }
}
