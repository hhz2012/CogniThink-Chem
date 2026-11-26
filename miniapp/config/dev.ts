import type { UserConfigExport } from '@tarojs/cli';
export default {
  logger: {
    quiet: false,
    stats: true,
  },
  mini: {},
  h5: {
    devServer: {
      open: false, //禁止自动打开浏览器
      // 浏览器端同源调用 /api，由 dev server 转发到后端 5080，规避跨域与沙箱端口限制
      proxy: {
        '/api': {
          target: 'http://localhost:5080',
          changeOrigin: true,
        },
      },
    },
  },
} satisfies UserConfigExport<'webpack5'>;
