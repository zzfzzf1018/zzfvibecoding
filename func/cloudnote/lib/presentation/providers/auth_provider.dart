import 'package:flutter/foundation.dart';

import '../../core/service_locator.dart';
import '../../services/github_auth_service.dart';

class AuthProvider extends ChangeNotifier {
  GitHubAuthService get _svc => ServiceLocator.auth;

  bool get isLoggedIn => _svc.isLoggedIn;
  String? get user => _svc.user;
  String? get token => _svc.token;

  Future<void> restore() async {
    await _svc.restore();
    notifyListeners();
  }

  Future<DeviceCodeResponse> startDeviceFlow() => _svc.requestDeviceCode();

  Future<void> awaitAuthorization(DeviceCodeResponse code) async {
    await _svc.pollForToken(code);
    notifyListeners();
  }

  Future<void> logout() async {
    await _svc.logout();
    notifyListeners();
  }
}
