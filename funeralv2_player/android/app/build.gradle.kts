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
                // `file(...)` 은 **이 모듈(android/app) 기준**으로 상대 경로를 푼다.
                // 그런데 key.properties 는 android/ 에 있고 릴리스 워크플로도
                // release.jks 를 그 옆(android/)에 놓는다. 그래서 file() 로 풀면
                // android/app/release.jks 를 찾다가 실패한다
                // ("Keystore file ... not found for signing config 'release'").
                // key.properties 와 같은 자리를 기준으로 풀도록 rootProject 를 쓴다.
                // 절대 경로를 적어 둔 경우에도 그대로 통한다.
                storeFile = rootProject.file(keystoreProperties["storeFile"] as String)
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

dependencies {
    // MainActivity 가 androidx.core.content.FileProvider 를 직접 쓴다 —
    // 받은 APK 를 시스템 설치 화면에 넘길 때 content:// URI 가 필요하다.
    //
    // Flutter 임베딩이 이미 androidx.core 를 끌어오지만, **우리 코드가 직접 부르는
    // 라이브러리는 명시해 둔다.** 끌려오는 버전이 바뀌어도 컴파일이 깨지지 않는다.
    implementation("androidx.core:core:1.13.1")
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
