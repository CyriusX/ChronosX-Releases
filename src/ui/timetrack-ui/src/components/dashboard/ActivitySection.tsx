import { Clock, ChevronRight, ChevronDown, GripVertical } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';

export function ActivitySection() {
  return (
    <div className="flex-1">
      <Card className="h-full bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl shadow-[0px_20px_25px_0px_rgba(0,0,0,0.1),0px_8px_10px_0px_rgba(0,0,0,0.1)]">
        <CardHeader className="pb-0 pt-[17px] px-[17px]">
          <CardTitle className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <ChevronRight className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              <span className="text-[14px] font-medium text-[rgba(245,247,251,0.9)]">Atividade</span>
            </div>
            <div className="flex items-center gap-1">
              <button className="w-5 h-5 flex items-center justify-center">
                <ChevronDown className="w-[14px] h-[14px] text-[rgba(245,247,251,0.4)]" />
              </button>
              <button className="w-5 h-5 flex items-center justify-center">
                <GripVertical className="w-[14px] h-[14px] text-[rgba(245,247,251,0.4)]" />
              </button>
            </div>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-4 pb-4 px-[17px] flex-1 flex items-center justify-center">
          <div className="text-center">
            <Clock className="w-12 h-12 mx-auto mb-3 text-[rgba(245,247,251,0.2)]" />
            <p className="text-[rgba(245,247,251,0.4)]">Nenhuma atividade registrada hoje</p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
