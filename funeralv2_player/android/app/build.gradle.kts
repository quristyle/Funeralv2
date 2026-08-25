import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// ─────────────────────────────────────────────────────────────
// [서명]
//
// 안드로이드는 서명되지 않은 APK 를 설치하지 않는다. 다만 **직접 만든 키가 없어도 된다** —
// Flutter 기본 설정은 release 를 디버그 키로 서명하고, 그 APK 는 정상 설치된다.
//
// 그런데 디버그 키는 기기·CI 러너마다 새로 만들어진다. 서명이 매번 달라지면
// 이미 깔린 앱 위에 업데이트가 거부된다(INSTALL_FAILED_UPDATE_INCOMPATIBLE).
// 키오스크는 현장에 설치된 뒤 계속 갱신해야 하므로 이게 실제로 걸린다.
//
// 그래서 **키가 있으면 그것으로, 없으면 디버그 키로** 서명한다.
//   · key.properties 가 있으면 → 그 키 (서명 고정 → 덮어쓰기 업데이트 가능)
//   · 없으면                   → 디버그 키 (설치는 되지만 업데이트는 삭제 후 재설치)
//
// CI 는 저장소 secrets 에 keystore 를 넣어 두면 자동으로 앞쪽을 탄다
// (.github/workflows/release.yml 의 android job).
// key.properties 와 .jks 는 절대 저장소에 넣지 않는다.
// ─────────────────────────────────────────────────────────────
val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")
val hasKeystore = keystorePropertiesFile.exists()
if (hasKeystore) {
    keystoreProperties.load(keystorePropertiesFile.inputStream())
}

android {
    namespace = "com.quristyle.funeralv2_player"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = "27.0.12077973" // 보다 안정적인 NDK 버전으로 고정

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.quristyle.funeralv2_player"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    signingConfigs {
        if (hasKeystore) {
            create("release") {
                keyAlias = keystoreProperties["keyAlias"] as String
                keyPassword = keystoreProperties["keyPassword"] as String
                storeFile = file(keystoreProperties["storeFile"] as String)
                storePassword = keystoreProperties["storePassword"] as String
            }
        }
    }

    buildTypes {
        release {
            // 위 주석 참고. 키가 있으면 그것으로, 없으면 디버그 키로 서명한다.
            // 어느 쪽이든 설치는 된다 — 차이는 '덮어쓰기 업데이트가 되는지' 다.
            signingConfig = if (hasKeystore) {
                signingConfigs.getByName("release")
            } else {
                signingConfigs.getByName("debug")
            }
        }
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
