import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../services/github_auth_service.dart';
import '../providers/auth_provider.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  DeviceCodeResponse? _code;
  String? _error;
  bool _busy = false;

  Future<void> _start() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final auth = context.read<AuthProvider>();
      final code = await auth.startDeviceFlow();
      setState(() => _code = code);
      await launchUrl(Uri.parse(code.verificationUri),
          mode: LaunchMode.externalApplication);
      await auth.awaitAuthorization(code);
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Sign in with GitHub')),
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.cloud_sync, size: 72),
                const SizedBox(height: 16),
                const Text(
                  'CloudNote stores notes in a private GitHub repository '
                  'using the standard git protocol.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 24),
                if (_code != null) ...[
                  SelectableText(
                    'Enter code: ${_code!.userCode}',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 8),
                  Text(_code!.verificationUri),
                  const SizedBox(height: 8),
                  const Text('Waiting for authorization…'),
                ],
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 16),
                    child: Text(_error!,
                        style: const TextStyle(color: Colors.red)),
                  ),
                const SizedBox(height: 24),
                FilledButton.icon(
                  onPressed: _busy ? null : _start,
                  icon: const Icon(Icons.login),
                  label: Text(_busy ? 'Working…' : 'Continue with GitHub'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
