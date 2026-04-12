export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        primary: { 50:'#e6f1fb', 100:'#b5d4f4', 500:'#378add', 700:'#185fa5', 900:'#042c53' },
        gov: { 50:'#f0f4f8', 800:'#1a365d', 900:'#0f2342' }
      },
      fontFamily: { arabic: ['Cairo', 'sans-serif'] }
    }
  },
  plugins: []
}
