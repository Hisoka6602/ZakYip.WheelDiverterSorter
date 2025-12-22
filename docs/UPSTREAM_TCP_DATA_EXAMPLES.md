# 发送给上游的 TCP JSON 数据示例

> 所有数据均为 JSON 格式，UTF-8 编码，每条消息以换行符 `\n` 结尾
> 
> 示例中使用统一的 ParcelId: `1733058000000`

---

## 1. ParcelDetectionNotification (包裹检测通知)

### 示例 1: 基本通知
```json
{"Type":"ParcelDetected","ParcelId":1733058000000,"DetectionTime":"2024-12-01T18:00:00.0000000+08:00","Metadata":null}
```

### 示例 2: 含元数据
```json
{"Type":"ParcelDetected","ParcelId":1733058000000,"DetectionTime":"2024-12-01T18:00:00.0000000+08:00","Metadata":{"SensorId":"PE001","LineId":"Line01"}}
```

---

## 2. SortingCompletedNotification (落格完成通知)

### 示例 1: 成功落格
```json
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":101,"CompletedAt":"2024-12-01T18:00:02.5000000+08:00","IsSuccess":true,"FinalStatus":"Success","FailureReason":null,"AffectedParcelIds":null}
```

### 示例 2: 分配超时
```json
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":999,"CompletedAt":"2024-12-01T18:00:08.0000000+08:00","IsSuccess":false,"FinalStatus":"Timeout","FailureReason":"ChuteAssignmentTimeout","AffectedParcelIds":null}
```

### 示例 3: 落格超时
```json
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":999,"CompletedAt":"2024-12-01T18:00:10.0000000+08:00","IsSuccess":false,"FinalStatus":"Timeout","FailureReason":"PathExecutionTimeout","AffectedParcelIds":null}
```

### 示例 4: 包裹丢失
```json
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":0,"CompletedAt":"2024-12-01T18:00:15.0000000+08:00","IsSuccess":false,"FinalStatus":"Lost","FailureReason":"ParcelLost_ExceededMaxSurvivalTime","AffectedParcelIds":[1733058001000,1733058002000]}
```
