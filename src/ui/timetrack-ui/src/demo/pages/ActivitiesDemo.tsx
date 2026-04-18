import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ActivitySection, BottomCards } from '../../components/dashboard';
import { useActivitiesData } from '../../hooks/useActivitiesData';
import {
  ActivitiesTopCards,
  DayInsights,
  DateNavigator,
  ProductivityHeatmap,
  MiniCalendar,
  SessionList,
  TopAppsPanel,
} from '../../components/activities';
import { ActivitiesFoldersCard } from '../../components/activities/ActivitiesFoldersCard';
import { useIpc } from '../../hooks/useIpc';
import { isDesktopRuntime } from '../../lib/runtime';
import { getDemoMode } from '../demoMode';

export default function ActivitiesDemo() {
  const { t } = useTranslation();
  const data = useActivitiesData();
  const { summary, activities, isLoading } = data;
  const { sendQuery, isConnected } = useIpc();
  const desktopRuntime = isDesktopRuntime();
  const [workGoalSeconds, setWorkGoalSeconds] = useState(28800);
  const demoMode = getDemoMode();

  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;
    sendQuery('getSettings').then((res) => {
      if (res.success && res.data) {
        const d = res.data as { workGoalSeconds?: number | null };
        if (d.workGoalSeconds) setWorkGoalSeconds(d.workGoalSeconds);
      }
    });
  }, [desktopRuntime, isConnected, sendQuery]);

  if (demoMode === 'mobile') {
    // Mobile-first: match the real Activities page structure so the demo feels
    // truthful and scrollable (vertical only).
    return (
      <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0 overflow-x-hidden">
        <main className="flex-1 flex flex-col min-w-0 min-h-0">
          <div className="px-5 pt-4 pb-2 flex-shrink-0">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="flex items-center gap-2 min-w-0">
                <span className="text-[14px] font-medium text-[#f5f7fb] truncate">
                  {t('activities.title')}
                </span>
                {isLoading && (
                  <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin flex-shrink-0" />
                )}
              </div>
              <DateNavigator
                selectedDate={data.selectedDate}
                isToday={data.isToday}
                onPrevDay={data.goToPrevDay}
                onNextDay={data.goToNextDay}
                onToday={data.goToToday}
                onDateSelect={data.setSelectedDate}
              />
            </div>
          </div>

          <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0">
            <div
              className="flex-1 flex flex-col gap-4 overflow-y-auto min-w-0 pr-1 overflow-x-hidden"
              data-demo-scroll-root="activities"
            >
              <DayInsights
                summary={summary}
                comparisonText={data.comparisonText}
                productivityComparison={data.productivityComparison}
                isToday={data.isToday}
              />

              <ActivitiesTopCards summary={summary} workGoalSeconds={workGoalSeconds} />

              {isLoading && activities.length === 0 ? (
                <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6 flex items-center justify-center">
                  <div className="flex items-center gap-3">
                    <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                    <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
                      {t('activities.loadingActivities')}
                    </span>
                  </div>
                </div>
              ) : (
                <ActivitySection activities={activities} selectedDate={data.selectedDate} />
              )}

              {isLoading && activities.length === 0 ? (
                <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl p-6 flex items-center justify-center">
                  <div className="flex items-center gap-3">
                    <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
                    <span className="text-[12px] text-[rgba(245,247,251,0.5)]">
                      {t('activities.loadingProductivity')}
                    </span>
                  </div>
                </div>
              ) : (
                <ProductivityHeatmap activities={activities} selectedDate={data.selectedDate} />
              )}

              <BottomCards summary={summary} />

              <SessionList activities={activities} defaultCollapsed />

              <div data-demo-marker="activities-bottom">
                <ActivitiesFoldersCard date={data.selectedDate} />

                <div className="lg:hidden flex flex-col gap-4 mt-4">
                  <MiniCalendar
                    selectedDate={data.selectedDate}
                    onDateSelect={data.setSelectedDate}
                    weeklyHistory={data.weeklyHistory}
                  />
                  <TopAppsPanel summary={summary} />
                </div>
              </div>
            </div>

            <div className="hidden lg:flex w-[280px] flex-shrink-0 flex-col gap-4 overflow-y-auto min-h-0">
              <MiniCalendar
                selectedDate={data.selectedDate}
                onDateSelect={data.setSelectedDate}
                weeklyHistory={data.weeklyHistory}
              />
              <TopAppsPanel summary={summary} />
            </div>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0">
      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        <div className="px-5 pt-4 pb-2 flex-shrink-0">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <span className="text-[14px] font-medium text-[#f5f7fb]">
                {t('activities.title')}
              </span>
              {isLoading && (
                <div className="w-4 h-4 border-2 border-[#8B5CF6] border-t-transparent rounded-full animate-spin" />
              )}
            </div>
            <DateNavigator
              selectedDate={data.selectedDate}
              isToday={data.isToday}
              onPrevDay={data.goToPrevDay}
              onNextDay={data.goToNextDay}
              onToday={data.goToToday}
              onDateSelect={data.setSelectedDate}
            />
          </div>
        </div>

        <div className="flex-1 flex gap-5 px-5 pb-4 min-h-0 overflow-hidden">
          <div className="flex-1 flex flex-col gap-4 min-w-0 min-h-0 overflow-hidden pr-1">
            <DayInsights
              summary={summary}
              comparisonText={data.comparisonText}
              productivityComparison={data.productivityComparison}
              isToday={data.isToday}
            />

            <ActivitiesTopCards summary={summary} workGoalSeconds={workGoalSeconds} />

            <div className="flex-1 min-h-0 overflow-hidden">
              <ActivitySection activities={activities} selectedDate={data.selectedDate} />
            </div>

            <div className="flex-shrink-0">
              <ProductivityHeatmap activities={activities} selectedDate={data.selectedDate} />
            </div>
          </div>

          <div className="hidden lg:flex w-[280px] flex-shrink-0 flex-col gap-4 overflow-hidden">
            <TopAppsPanel summary={summary} />
          </div>
        </div>
      </main>
    </div>
  );
}
