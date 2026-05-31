import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/notes_provider.dart';
import '../screens/editor_screen.dart';

class NoteList extends StatelessWidget {
  const NoteList({super.key});

  @override
  Widget build(BuildContext context) {
    final notes = context.watch<NotesProvider>();
    if (notes.loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (notes.notes.isEmpty) {
      return const Center(child: Text('No notes yet. Tap + to create one.'));
    }
    return ListView.separated(
      itemCount: notes.notes.length,
      separatorBuilder: (_, __) => const Divider(height: 1),
      itemBuilder: (ctx, i) {
        final n = notes.notes[i];
        return ListTile(
          title: Text(n.title, maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text(
            n.body.split('\n').take(1).join(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          trailing: PopupMenuButton<String>(
            onSelected: (v) async {
              if (v == 'delete') await notes.delete(n.id);
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'delete', child: Text('Delete')),
            ],
          ),
          onTap: () {
            notes.select(n);
            Navigator.of(ctx).push(
              MaterialPageRoute(builder: (_) => EditorScreen(note: n)),
            );
          },
        );
      },
    );
  }
}
