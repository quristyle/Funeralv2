package com.quristyle.funeralv2_player

import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.net.NetworkInterface
import java.util.Collections
import java.util.Locale

class MainActivity : FlutterActivity() {
    private val CHANNEL = "com.quristyle.funeralv2_player/device_info"

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
