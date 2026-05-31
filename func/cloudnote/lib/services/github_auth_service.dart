import 'dart:async';
import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

import '../core/config.dart';

/// GitHub OAuth Device Flow. Works on both desktop (Windows) and mobile
/// (Android) without needing a redirect URI / embedded webview.
///
/// Reference: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps#device-flow
class DeviceCodeResponse {
  DeviceCodeResponse({
    required this.deviceCode,
    required this.userCode,
    required this.verificationUri,
    required this.interval,
    required this.expiresIn,
  });

  final String deviceCode;
  final String userCode;
  final String verificationUri;
  final int interval;
  final int expiresIn;
}

class GitHubAuthService {
  GitHubAuthService({
    required FlutterSecureStorage secure,
    http.Client? client,
  })  : _secure = secure,
        _client = client ?? http.Client();

  static const _tokenKey = 'gh_token';
  static const _userKey = 'gh_user';

  final FlutterSecureStorage _secure;
  final http.Client _client;

  String? _token;
  String? _user;

  String? get token => _token;
  String? get user => _user;
  bool get isLoggedIn => _token != null && _token!.isNotEmpty;

  Future<void> restore() async {
    _token = await _secure.read(key: _tokenKey);
    _user = await _secure.read(key: _userKey);
  }

  Future<DeviceCodeResponse> requestDeviceCode() async {
    final res = await _client.post(
      Uri.parse('https://github.com/login/device/code'),
      headers: {'Accept': 'application/json'},
      body: {
        'client_id': AppConfig.githubClientId,
        'scope': AppConfig.githubScopes.join(' '),
      },
    );
    if (res.statusCode != 200) {
      throw StateError('Device code request failed: ${res.statusCode}');
    }
    final j = jsonDecode(res.body) as Map<String, dynamic>;
    return DeviceCodeResponse(
      deviceCode: j['device_code'] as String,
      userCode: j['user_code'] as String,
      verificationUri: j['verification_uri'] as String,
      interval: (j['interval'] as num).toInt(),
      expiresIn: (j['expires_in'] as num).toInt(),
    );
  }

  /// Poll until the user authorizes (or timeout). Returns the access token.
  Future<String> pollForToken(DeviceCodeResponse code) async {
    final deadline = DateTime.now().add(Duration(seconds: code.expiresIn));
    var interval = code.interval;
    while (DateTime.now().isBefore(deadline)) {
      await Future<void>.delayed(Duration(seconds: interval));
      final res = await _client.post(
        Uri.parse('https://github.com/login/oauth/access_token'),
        headers: {'Accept': 'application/json'},
        body: {
          'client_id': AppConfig.githubClientId,
          'device_code': code.deviceCode,
          'grant_type': 'urn:ietf:params:oauth:grant-type:device_code',
        },
      );
      final j = jsonDecode(res.body) as Map<String, dynamic>;
      if (j['access_token'] is String) {
        final tok = j['access_token'] as String;
        await _persist(tok);
        return tok;
      }
      final err = j['error'] as String?;
      if (err == 'authorization_pending') continue;
      if (err == 'slow_down') {
        interval += 5;
        continue;
      }
      throw StateError('Auth failed: $err');
    }
    throw TimeoutException('User did not authorize in time');
  }

  Future<void> _persist(String tok) async {
    _token = tok;
    await _secure.write(key: _tokenKey, value: tok);
    final res = await _client.get(
      Uri.parse('https://api.github.com/user'),
      headers: {
        'Authorization': 'Bearer $tok',
        'Accept': 'application/vnd.github+json',
      },
    );
    if (res.statusCode == 200) {
      final j = jsonDecode(res.body) as Map<String, dynamic>;
      _user = j['login'] as String?;
      if (_user != null) await _secure.write(key: _userKey, value: _user);
    }
  }

  Future<void> logout() async {
    _token = null;
    _user = null;
    await _secure.delete(key: _tokenKey);
    await _secure.delete(key: _userKey);
  }
}
