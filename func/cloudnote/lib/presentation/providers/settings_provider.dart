import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:shared_preferences/shared_preferences.dart';

class SettingsProvider extends ChangeNotifier {
  static const _kFontFamily = 'fontFamily';
  static const _kFontScale = 'fontScale';
  static const _kFontColor = 'fontColor';
  static const _kParagraphSpacing = 'paragraphSpacing';
  static const _kThemeMode = 'themeMode';
  static const _kAutoSync = 'autoSync';
  static const _kSyncInterval = 'syncIntervalMinutes';
  static const _kActiveRepo = 'activeRepoJson';

  String fontFamily = 'Roboto';
  double fontScale = 1.0;
  Color fontColor = const Color(0xFF222222);
  double paragraphSpacing = 1.4;
  ThemeMode themeMode = ThemeMode.system;
  bool autoSync = true;
  int syncIntervalMinutes = 10;
  String? activeRepoJson;

  Iterable<String> get availableFonts =>
      GoogleFonts.asMap().keys.take(40); // capped for UI

  Future<void> load() async {
    final p = await SharedPreferences.getInstance();
    fontFamily = p.getString(_kFontFamily) ?? fontFamily;
    fontScale = p.getDouble(_kFontScale) ?? fontScale;
    final c = p.getInt(_kFontColor);
    if (c != null) fontColor = Color(c);
    paragraphSpacing = p.getDouble(_kParagraphSpacing) ?? paragraphSpacing;
    final tm = p.getString(_kThemeMode);
    themeMode = ThemeMode.values.firstWhere(
      (e) => e.name == tm,
      orElse: () => ThemeMode.system,
    );
    autoSync = p.getBool(_kAutoSync) ?? autoSync;
    syncIntervalMinutes = p.getInt(_kSyncInterval) ?? syncIntervalMinutes;
    activeRepoJson = p.getString(_kActiveRepo);
    notifyListeners();
  }

  Future<void> _persist() async {
    final p = await SharedPreferences.getInstance();
    await p.setString(_kFontFamily, fontFamily);
    await p.setDouble(_kFontScale, fontScale);
    await p.setInt(_kFontColor, fontColor.value);
    await p.setDouble(_kParagraphSpacing, paragraphSpacing);
    await p.setString(_kThemeMode, themeMode.name);
    await p.setBool(_kAutoSync, autoSync);
    await p.setInt(_kSyncInterval, syncIntervalMinutes);
    if (activeRepoJson != null) {
      await p.setString(_kActiveRepo, activeRepoJson!);
    }
  }

  void update({
    String? fontFamily,
    double? fontScale,
    Color? fontColor,
    double? paragraphSpacing,
    ThemeMode? themeMode,
    bool? autoSync,
    int? syncIntervalMinutes,
    String? activeRepoJson,
  }) {
    if (fontFamily != null) this.fontFamily = fontFamily;
    if (fontScale != null) this.fontScale = fontScale;
    if (fontColor != null) this.fontColor = fontColor;
    if (paragraphSpacing != null) this.paragraphSpacing = paragraphSpacing;
    if (themeMode != null) this.themeMode = themeMode;
    if (autoSync != null) this.autoSync = autoSync;
    if (syncIntervalMinutes != null) {
      this.syncIntervalMinutes = syncIntervalMinutes;
    }
    if (activeRepoJson != null) this.activeRepoJson = activeRepoJson;
    _persist();
    notifyListeners();
  }
}
