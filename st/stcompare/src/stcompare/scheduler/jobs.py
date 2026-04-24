from __future__ import annotations

import logging

from apscheduler.schedulers.blocking import BlockingScheduler
from apscheduler.triggers.cron import CronTrigger

from stcompare.config import AppConfig
from stcompare.service import MonitorService


LOGGER = logging.getLogger(__name__)


def start_scheduler(service: MonitorService, app_config: AppConfig) -> None:
    scheduler = BlockingScheduler(timezone=app_config.app.timezone)
    schedule = app_config.schedule

    def _run_job() -> None:
        summary = service.run_once()
        LOGGER.info(
            "Scheduled run finished with %s inserted records, %s comparisons, %s errors",
            summary.inserted_records,
            len(summary.comparisons),
            len(summary.errors),
        )

    if schedule.mode == "interval":
        scheduler.add_job(
            _run_job,
            "interval",
            minutes=schedule.minutes,
            max_instances=schedule.max_instances,
            coalesce=schedule.coalesce,
        )
    elif schedule.mode == "cron" and schedule.cron:
        scheduler.add_job(
            _run_job,
            CronTrigger.from_crontab(schedule.cron, timezone=app_config.app.timezone),
            max_instances=schedule.max_instances,
            coalesce=schedule.coalesce,
        )
    else:
        raise ValueError("Unsupported scheduler configuration")

    LOGGER.info("Scheduler started with mode=%s", schedule.mode)
    _run_job()
    scheduler.start()
