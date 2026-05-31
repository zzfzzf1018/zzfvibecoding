package com.lisb.reader.tts

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/** Receives notification button clicks and forwards them to [TtsService]. */
class TtsActionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val action = intent.action ?: return
        val svc = Intent(context, TtsService::class.java).setAction(action)
        // Use startService rather than startForegroundService: the service is
        // already in the foreground when the user interacts with its notification.
        context.startService(svc)
    }
}
