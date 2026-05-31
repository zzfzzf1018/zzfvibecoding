import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/theme.dart';
import 'presentation/providers/auth_provider.dart';
import 'presentation/providers/settings_provider.dart';
import 'presentation/screens/home_screen.dart';
import 'presentation/screens/login_screen.dart';

class CloudNoteApp extends StatelessWidget {
  const CloudNoteApp({super.key});

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SettingsProvider>();
    final auth = context.watch<AuthProvider>();

    return MaterialApp(
      title: 'CloudNote',
      debugShowCheckedModeBanner: false,
      theme: buildLightTheme(settings),
      darkTheme: buildDarkTheme(settings),
      themeMode: settings.themeMode,
      home: auth.isLoggedIn ? const HomeScreen() : const LoginScreen(),
    );
  }
}
