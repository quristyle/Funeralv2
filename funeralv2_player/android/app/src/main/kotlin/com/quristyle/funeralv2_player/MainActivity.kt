package com.quristyle.funeralv2_player

import android.content.Intent
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.content.FileProvider
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File
import java.net.NetworkInterface
import java.util.Collections
import java.util.Locale

class MainActivity : FlutterActivity() {
    private val CHANNEL = "com.quristyle.funeralv2_player/device_info"

    /// 새 버전 설치용 채널. lib/services/update/update_service.dart 가 부른다.
    private val UPDATE_CHANNEL = "com.quristyle.funeralv2_player/update"

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            if (call.method == "getMacAddress") {
                val mac = getMacAddress()
                if (mac != null) {
                    result.success(mac)
                } else {
                    result.error("UNAVAILABLE", "MAC address not available.", null)
                }
            } else {
                result.notImplemented()
            }
        }

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, UPDATE_CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "installAllowed" -> result.success(installAllowed())
                "openInstallSettings" -> {
                    try {
                        openInstallSettings()
                        result.success(null)
                    } catch (e: Exception) {
                        result.error("SETTINGS_FAILED", e.message, null)
                    }
                }
                "installApk" -> {
                    val path = call.argument<String>("path")
                    if (path.isNullOrEmpty()) {
                        result.error("NO_PATH", "설치 파일 경로가 없습니다.", null)
                    } else {
                        try {
                            installApk(path)
                            result.success(null)
                        } catch (e: Exception) {
                            result.error("INSTALL_FAILED", e.message, null)
                        }
                    }
                }
                else -> result.notImplemented()
            }
        }
    }

    /**
     * 이 앱에 "알 수 없는 앱 설치" 가 허용되어 있는지.
     *
     * API 26 부터는 앱별 권한이라 물어볼 수 있다. 그 이전은 기기 전체 설정이어서
     * 앱이 알 방법이 없으므로 허용된 것으로 본다 — 막혀 있으면 설치 화면 대신
     * 시스템이 안내를 띄운다.
     */
    private fun installAllowed(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            packageManager.canRequestPackageInstalls()
        } else {
            true
        }
    }

    /** "알 수 없는 앱 설치" 허용 설정 화면을 연다. */
    private fun openInstallSettings() {
        val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Intent(
                Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:$packageName")
            )
        } else {
            Intent(Settings.ACTION_SECURITY_SETTINGS)
        }
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        startActivity(intent)
    }

    /**
     * 받아 둔 APK 의 시스템 설치 화면을 띄운다.
     *
     * **조용히 설치되지 않는다.** 시스템 앱이 아닌 앱은 사용자 확인 없이 패키지를
     * 깔 수 없다. 화면에 뜨는 확인을 사람이 눌러야 한다(TV 박스는 리모컨).
     *
     * API 24 부터 `file://` URI 를 다른 앱에 넘기면 FileUriExposedException 이 난다.
     * 그래서 FileProvider 로 `content://` URI 를 만들어 넘기고, 읽기 권한을
     * 인텐트에 함께 실어 준다.
     */
    private fun installApk(path: String) {
        val file = File(path)
        if (!file.exists()) {
            throw IllegalStateException("설치 파일이 없습니다: $path")
        }
        val uri = FileProvider.getUriForFile(this, "$packageName.fileprovider", file)
        val intent = Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        startActivity(intent)
    }

    private fun getMacAddress(): String? {
        try {
            val interfaces = Collections.list(NetworkInterface.getNetworkInterfaces())
            for (intf in interfaces) {
                // 이더넷(eth) 또는 와이파이(wlan) 인터페이스 검색
                val name = intf.name.lowercase(Locale.ROOT)
                if (name.contains("wlan") || name.contains("eth")) {
                    val mac = intf.hardwareAddress ?: continue
                    val buf = StringBuilder()
                    for (b in mac) {
                        buf.append(String.format("%02X:", b))
                    }
                    if (buf.length > 0) {
                        buf.deleteCharAt(buf.length - 1)
                    }
                    val macStr = buf.toString()
                    if (macStr != "02:00:00:00:00:00" && macStr.isNotEmpty()) {
                        return macStr
                    }
                }
            }
        } catch (ex: Exception) {
            ex.printStackTrace()
        }
        return null
    }
}
