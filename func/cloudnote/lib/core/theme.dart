import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

import '../presentation/providers/settings_provider.dart';

TextTheme _textTheme(SettingsProvider s, TextTheme base) {
  final t = GoogleFonts.getTextTheme(s.fontFamily, base);
  return t.apply(
    bodyColor: s.fontColor,
    displayColor: s.fontColor,
    fontSizeFactor: s.fontScale,
  );
}

ThemeData buildLightTheme(SettingsProvider s) {
  final base = ThemeData.light(useMaterial3: true);
  return base.copyWith(
    textTheme: _textTheme(s, base.textTheme),
    colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
  );
}

ThemeData buildDarkTheme(SettingsProvider s) {
  final base = ThemeData.dark(useMaterial3: true);
  return base.copyWith(
    textTheme: _textTheme(s, base.textTheme),
    colorScheme: ColorScheme.fromSeed(
      seedColor: Colors.indigo,
      brightness: Brightness.dark,
    ),
  );
}
