import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:provider/provider.dart';

import '../../core/service_locator.dart';
import '../../domain/entities/note_repo.dart';
import '../providers/auth_provider.dart';
import '../providers/notes_provider.dart';
import '../providers/settings_provider.dart';
import '../widgets/note_list.dart';
import 'editor_screen.dart';
import 'settings_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  bool _initialising = true;
  String? _status;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _bootstrap());
  }

  Future<void> _bootstrap() async {
    final settings = context.read<SettingsProvider>();
    final notes = context.read<NotesProvider>();

    if (settings.activeRepoJson != null) {
      final repo = NoteRepo.fromJson(
        jsonDecode(settings.activeRepoJson!) as Map<String, dynamic>,
      );
      notes.bindRepository(repo.localPath);
    }
    if (mounted) setState(() => _initialising = false);
  }

  Future<void> _createOrPickRepo() async {
    final auth = context.read<AuthProvider>();
    final settings = context.read<SettingsProvider>();
    final notes = context.read<NotesProvider>();

    final nameController = TextEditingController(text: 'my-cloudnote');
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Create notes repository'),
        content: TextField(
          controller: nameController,
          decoration: const InputDecoration(labelText: 'Repository name'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Create'),
          ),
        ],
      ),
    );
    if (ok != true) return;

    setState(() => _status = 'Creating repository on GitHub…');
    try {
      final repo = await ServiceLocator.api.createRepo(
        name: nameController.text.trim(),
      );
      final docs = await getApplicationDocumentsDirectory();
      final localPath = p.join(docs.path, 'CloudNote', repo.name);
      setState(() => _status = 'Cloning…');
      await ServiceLocator.git.clone(
        url: repo.cloneUrl,
        dest: localPath,
        token: auth.token,
      );
      final stored = NoteRepo(
        name: repo.name,
        owner: repo.owner,
        cloneUrl: repo.cloneUrl,
        localPath: localPath,
        isPrivate: repo.isPrivate,
      );
      settings.update(activeRepoJson: jsonEncode(stored.toJson()));
      notes.bindRepository(localPath);
      setState(() => _status = null);
    } catch (e) {
      setState(() => _status = 'Failed: $e');
    }
  }

  Future<void> _syncNow() async {
    final settings = context.read<SettingsProvider>();
    final auth = context.read<AuthProvider>();
    if (settings.activeRepoJson == null || auth.token == null) return;
    final repo = NoteRepo.fromJson(
      jsonDecode(settings.activeRepoJson!) as Map<String, dynamic>,
    );
    setState(() => _status = 'Syncing…');
    final result = await ServiceLocator.sync.syncNow(
      repo: repo,
      token: auth.token!,
      authorName: auth.user ?? 'cloudnote',
      authorEmail: '${auth.user ?? 'cloudnote'}@users.noreply.github.com',
    );
    setState(() =>
        _status = result.message == null ? 'Synced' : 'Sync failed: ${result.message}');
  }

  @override
  Widget build(BuildContext context) {
    final notes = context.watch<NotesProvider>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('CloudNote'),
        actions: [
          IconButton(
            tooltip: 'Sync now',
            onPressed: notes.hasRepo ? _syncNow : null,
            icon: const Icon(Icons.sync),
          ),
          IconButton(
            tooltip: 'Settings',
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const SettingsScreen()),
            ),
            icon: const Icon(Icons.settings),
          ),
        ],
      ),
      floatingActionButton: notes.hasRepo
          ? FloatingActionButton(
              onPressed: () {
                final draft = notes.createDraft();
                Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => EditorScreen(note: draft),
                  ),
                );
              },
              child: const Icon(Icons.add),
            )
          : null,
      body: _initialising
          ? const Center(child: CircularProgressIndicator())
          : !notes.hasRepo
              ? _NoRepoView(onCreate: _createOrPickRepo, status: _status)
              : Column(
                  children: [
                    if (_status != null)
                      Container(
                        width: double.infinity,
                        color: Theme.of(context).colorScheme.surfaceContainerHighest,
                        padding: const EdgeInsets.all(8),
                        child: Text(_status!),
                      ),
                    const Expanded(child: NoteList()),
                  ],
                ),
    );
  }
}

class _NoRepoView extends StatelessWidget {
  const _NoRepoView({required this.onCreate, this.status});
  final VoidCallback onCreate;
  final String? status;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.book_outlined, size: 72),
          const SizedBox(height: 12),
          const Text('No notebook yet.'),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: onCreate,
            icon: const Icon(Icons.add),
            label: const Text('Create GitHub notebook'),
          ),
          if (status != null) ...[
            const SizedBox(height: 16),
            Text(status!),
          ],
        ],
      ),
    );
  }
}
