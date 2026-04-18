# FuckETS 去他妈的讯飞E听说

讯飞E听说高中版试题答案获取


## 环境要求

- Windows 10 / 11
- Python 3.10 或更高版本

## 安装依赖

```
pip install -r requirements.txt
```

## 使用方法

首先，在电脑版的E听说上下载要解析的试题
然后打开命令提示符,定位到mian.py所在目录，输入以下命令：  
```
python main.py
```
如果电脑未安装Python，也可下载并使用Releases中的文件

## 注意事项

目前仅测试了**广东地区**的试题

## 项目结构
```
FuckETS/
├── main.py            # 程序入口
├── app.py             # GUI 主窗口
├── parser.py          # JSON 解析逻辑
├── pdf_generator.py   # PDF 生成
├── utils.py           # 工具函数
├── requirements.txt   # 依赖列表
└── setup.cmd          # 一键打包脚本
```

## 许可

MIT License