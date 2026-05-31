import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
import '../providers/settings_provider.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SettingsProvider>();
    final auth = context.watch<AuthProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        children: [
          const _SectionHeader('Appearance'),
          ListTile(
            title: const Text('Theme'),
            trailing: DropdownButton<ThemeMode>(
              value: settings.themeMode,
              onChanged: (v) => settings.update(themeMode: v),
              items: const [
                DropdownMenuItem(value: ThemeMode.system, child: Text('System')),
                DropdownMenuItem(value: ThemeMode.light, child: Text('Light')),
                DropdownMenuItem(value: ThemeMode.dark, child: Text('Dark')),
              ],
            ),
          ),
          ListTile(
            title: const Text('Font family'),
            trailing: DropdownButton<String>(
              value: settings.fontFamily,
              onChanged: (v) => v == null ? null : settings.update(fontFamily: v),
              items: settings.availableFonts
                  .map((f) => DropdownMenuItem(value: f, child: Text(f)))
                  .toList(),
            ),
          ),
          ListTile(
            title: const Text('Font size'),
            subtitle: Slider(
              min: 0.8,
              max: 2.0,
              divisions: 12,
              value: settings.fontScale,
              label: settings.fontScale.toStringAsFixed(2),
              onChanged: (v) => settings.update(fontScale: v),
            ),
          ),
          ListTile(
            title: const Text('Paragraph spacing'),
            subtitle: Slider(
              min: 1.0,
              max: 2.5,
              divisions: 15,
              value: settings.paragraphSpacing,
              label: settings.paragraphSpacing.toStringAsFixed(2),
              onChanged: (v) => settings.update(paragraphSpacing: v),
            ),
          ),
          ListTile(
            title: const Text('Font color'),
            trailing: Wrap(
              spacing: 6,
              children: const [
                Color(0xFF222222),
                Color(0xFF1565C0),
                Color(0xFF2E7D32),
                Color(0xFFB71C1C),
                Color(0xFF6A1B9A),
              ]
                  .map((c) => GestureDetector(
                        onTap: () => settings.update(fontColor: c),
                        child: CircleAvatar(backgroundColor: c, radius: 12),
                      ))
                  .toList(),
            ),
          ),
          const _SectionHeader('Sync'),
          SwitchListTile(
            title: const Text('Auto sync'),
            value: settings.autoSync,
            onChanged: (v) => settings.update(autoSync: v),
          ),
          ListTile(
            title: const Text('Auto sync interval (minutes)'),
            trailing: DropdownButton<int>(
              value: settings.syncIntervalMinutes,
              onChanged: (v) => v == null
                  ? null
                  : settings.update(syncIntervalMinutes: v),
              items: const [1, 5, 10, 15, 30, 60]
                  .map((m) => DropdownMenuItem(value: m, child: Text('$m')))
                  .toList(),
            ),
          ),
          const _SectionHeader('Account'),
          ListTile(
            title: Text('GitHub: ${auth.user ?? "not signed in"}'),
            trailing: TextButton(
              onPressed: () => auth.logout(),
              child: const Text('Sign out'),
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
      child: Text(
        text,
        style: Theme.of(context).textTheme.labelLarge?.copyWith(
              color: Theme.of(context).colorScheme.primary,
            ),
      ),
    );
  }
}
